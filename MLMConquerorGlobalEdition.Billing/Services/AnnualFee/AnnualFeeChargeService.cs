using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Services.AnnualFee;

/// <summary>
/// El cobro de la cuota anual (Plan K). Tres formas de pagar, cada una bajo el candado de fondos del
/// miembro y con un AnnualFeePayment que ocupa el ciclo (UX_AnnualFeePayments_LivePerCycle):
///
///   • TOKEN: una transacción. Se gasta el token, se crea el pedido de 0, se completa el pago y avanza
///     el estado. Siempre que haya uno (K6).
///   • COMISIONES: una transacción. Débito + PaymentHistory (FundAnnualFeeAsync) con el saldo GASTABLE
///     (sin lo reservado por un pago ni lo aún no pagable), pedido de 99 y estado.
///     En automático, solo con la preferencia FeesFromBalance (K7).
///   • EWALLET: dos fases, como la compra de tokens (archivo AnnualFeeChargeService.EWallet.cs).
///
/// Bajo el candado se relee el estado y se comprueba que el ciclo no esté ya pagado, en curso o en
/// revisión: un "Pagar ahora" y el motor no pueden cobrar el mismo ciclo. Un fallo sin fondos aplica la
/// cadencia del plan; nunca toca la membresía (K12).
/// </summary>
public sealed partial class AnnualFeeChargeService : IAnnualFeeChargeService
{
    private const string AutoActor = "annual-fee-engine";

    /// <summary>Interno: la clave de "Pagar ahora" ya tiene un pago; lo contesta la repetición.</summary>
    private const string ReplayCode = "ANNUAL_FEE_REPLAY";

    private readonly AppDbContext                    _db;
    private readonly IDateTimeProvider               _clock;
    private readonly IMemberFundsGate                _gate;
    private readonly ICommissionBalanceService       _commissions;
    private readonly IEWalletFunds                   _eWallet;
    private readonly IAnnualFeeTokenConsumer         _tokens;
    private readonly IAnnualFeeStateEnrollment       _enrollment;
    private readonly IRecurringBillingScheduler      _scheduler;
    private readonly IConfiguration                  _config;
    private readonly ILogger<AnnualFeeChargeService> _logger;

    public AnnualFeeChargeService(
        AppDbContext db, IDateTimeProvider clock, IMemberFundsGate gate, ICommissionBalanceService commissions,
        IEWalletFunds eWallet, IAnnualFeeTokenConsumer tokens, IAnnualFeeStateEnrollment enrollment,
        IRecurringBillingScheduler scheduler, IConfiguration config, ILogger<AnnualFeeChargeService> logger)
    {
        _db          = db;
        _clock       = clock;
        _gate        = gate;
        _commissions = commissions;
        _eWallet     = eWallet;
        _tokens      = tokens;
        _enrollment  = enrollment;
        _scheduler   = scheduler;
        _config      = config;
        _logger      = logger;
    }

    /// <summary>Se lee en cada llamada: apagarlo no exige reiniciar Billing (K13).</summary>
    private bool AutoChargeEnabled => _config.GetValue(AnnualFeeRules.AutoChargeEnabledConfigKey, false);

    // ── Cobro automático ──────────────────────────────────────────────────────

    public async Task<Result<AnnualFeeChargeOutcome>> ChargeDueAsync(string billingStateId, bool force, CancellationToken ct = default)
    {
        if (!force && !AutoChargeEnabled)
            return Skipped(AnnualFeeErrors.AutoChargeDisabled);

        var state = await _db.SubscriptionBillingStates
            .Include(s => s.Plan).ThenInclude(p => p!.PlanProducts)
            .FirstOrDefaultAsync(s => s.Id == billingStateId, ct);
        if (state is null || state.Purpose != BillingStatePurpose.AnnualFee || state.Plan is not { IsAnnualFeePlan: true })
            return Result<AnnualFeeChargeOutcome>.Failure(AnnualFeeErrors.StateNotFound,
                $"Annual-fee billing state '{billingStateId}' not found.");

        if (state.Status == RecurringBillingStatus.Stopped && !force)
            return Skipped("Billing state is Stopped.");

        var now    = _clock.Now;
        var member = await _db.MemberProfiles.AsNoTracking().FirstOrDefaultAsync(m => m.MemberId == state.MemberId, ct);

        var decision = AnnualFeeEligibility.ForAutomaticCharge(
            await AnnualFeeMemberQueries.FactsAsync(_db, state.MemberId, ct), state.NextBillingDate, state.LastSuccessfulBillingDate, now, force);
        if (decision.Kind != AnnualFeeDecisionKind.Charge)
        {
            ApplyDecision(state, decision, now);
            await _db.SaveChangesAsync(ct);
            return Skipped(decision.Reason);
        }

        var price = await AnnualFeePlanQueries.PriceAsync(_db, state.Plan, ct);
        if (price is null)
            return Result<AnnualFeeChargeOutcome>.Failure(AnnualFeeErrors.PriceNotSet, "The annual-fee plan has no price.");

        if (force && state.Status == RecurringBillingStatus.Stopped)
        {
            // Como el procesador: forzar revive un estado parado. Se guarda ya, porque bajo el candado el
            // estado se relee de la base y un cambio sin guardar se perdería.
            state.Status              = RecurringBillingStatus.Active;
            state.CurrentAttemptIndex = 0;
            state.LastUpdateDate      = now;
            state.LastUpdateBy        = AutoActor;
            await _db.SaveChangesAsync(ct);
        }

        var rule = force ? DueRule.Any : DueRule.Due;
        try
        {
            // 1. Un token de cuota, si lo hay: siempre (K6).
            var settled = await SettleWithTokenAsync(state, price, AnnualFeePaymentSource.Automatic, null, AutoActor, rule, ct);

            // 2. Comisiones y 3. eWallet, solo con la preferencia (K7).
            if (settled.Kind == SettleKind.NotPossible && member!.FeesFromBalance)
                settled = await SettleWithCommissionsAsync(state, price, AnnualFeePaymentSource.Automatic, null, AutoActor, rule, ct);
            if (settled.Kind == SettleKind.NotPossible && member!.FeesFromBalance)
                settled = await SettleWithEWalletAsync(state, price, AnnualFeePaymentSource.Automatic, null, AutoActor, rule, ct);

            if (settled.Kind != SettleKind.NotPossible)
                return Outcome(settled);
        }
        catch (MemberFundsBusyException)
        {
            return Result<AnnualFeeChargeOutcome>.Failure(AnnualFeeErrors.Busy,
                $"Funds of member {state.MemberId} are busy; the annual fee will be retried on the next pass.");
        }

        return await RecordNoFundsAsync(state, price, member!, ct);
    }

    // ── Formas de pago (bajo el candado) ─────────────────────────────────────

    private enum DueRule { Due, PayAhead, Any }

    private enum SettleKind { Paid, NotPossible, Blocked, Review }

    private sealed record Settled(
        SettleKind Kind, string? Code = null, AnnualFeePayment? Payment = null, Orders? Order = null, string? FundingSource = null)
    {
        public static Settled Paid(AnnualFeePayment p, Orders o, string source)   => new(SettleKind.Paid, null, p, o, source);
        public static Settled NotPossible(string code)                             => new(SettleKind.NotPossible, code);
        public static Settled Blocked(string code)                                 => new(SettleKind.Blocked, code);
        public static Settled Review(AnnualFeePayment p, Orders o, string source) => new(SettleKind.Review, AnnualFeeErrors.NeedsReview, p, o, source);
    }

    private async Task<Settled> SettleWithTokenAsync(
        SubscriptionBillingState state, AnnualFeePrice price, AnnualFeePaymentSource source, string? key,
        string actor, DueRule rule, CancellationToken ct)
    {
        var now    = _clock.Now;
        var before = Tracked();
        await using var tx = await _gate.EnterAsync(state.MemberId, ct);
        try
        {

            var blocked = await BlockedUnderGateAsync(state, key, rule, now, ct);
            if (blocked is not null)
            {
                await SafeRollbackAsync(tx, state.MemberId);
                return Settled.Blocked(blocked);
            }

            var instance = await _tokens.FindAsync(state.MemberId, now, ct);
            if (instance is null)
            {
                await SafeRollbackAsync(tx, state.MemberId);
                return Settled.NotPossible(AnnualFeeErrors.NoFeeToken);
            }

            var cycle   = state.NextBillingDate.Date;
            var order   = NewOrder(state.MemberId, 0m, cycle, actor, now);
            var payment = NewPayment(state, cycle, 0m, AnnualFeePaymentMethod.Token, source, key, order, actor, now);
            var token   = await _tokens.ConsumeAsync(instance, order.Id, actor, now, ct);

            order.Notes = $"{order.Notes} — paid with token {token.Code}";
            payment.CompleteWithToken(token.TokenTransactionId, token.Code, now, actor);
            MarkPaid(order, actor, now);
            AdvanceOnSuccess(state, cycle, now, now, actor);
            AddAttempt(state, price.ProductId, RecurringFundingSource.Token, RecurringAttemptOutcome.Success, 0m,
                order.Id, paymentHistoryId: null, token.TokenTransactionId, commissionDebitId: null, failureReason: null, now, actor);

            return await CommitOrBlockAsync(tx, Settled.Paid(payment, order, AnnualFeeMethods.Token), key, before, ct);
        }
        catch (Exception)
        {
            // Revisión C-1: cualquier otra excepción bajo el candado (no un choque de índice, que se
            // trata dentro) deshace, suelta lo añadido y relee lo tocado, y se relanza. El contexto
            // lo comparte el trabajador: su siguiente SaveChanges no puede guardar nada a medias.
            await AbortUnderGateAsync(tx, state.MemberId, before);
            throw;
        }
    }

    private async Task<Settled> SettleWithCommissionsAsync(
        SubscriptionBillingState state, AnnualFeePrice price, AnnualFeePaymentSource source, string? key,
        string actor, DueRule rule, CancellationToken ct)
    {
        var now    = _clock.Now;
        var before = Tracked();
        await using var tx = await _gate.EnterAsync(state.MemberId, ct);
        try
        {

            var blocked = await BlockedUnderGateAsync(state, key, rule, now, ct);
            if (blocked is not null)
            {
                await SafeRollbackAsync(tx, state.MemberId);
                return Settled.Blocked(blocked);
            }

            var spendable = await _commissions.GetSpendableAsync(state.MemberId, ct);
            if (spendable < price.Amount)
            {
                await SafeRollbackAsync(tx, state.MemberId);
                return Settled.NotPossible(AnnualFeeErrors.InsufficientCommissionBalance);
            }

            var cycle   = state.NextBillingDate.Date;
            var order   = NewOrder(state.MemberId, price.Amount, cycle, actor, now);
            var payment = NewPayment(state, cycle, price.Amount, AnnualFeePaymentMethod.CommissionBalance, source, key, order, actor, now);

            Result<CommissionDebit> fund;
            try
            {
                // Guarda a la vez el pedido, el pago (aún Pending), el débito y su PaymentHistory.
                fund = await _commissions.FundAnnualFeeAsync(state.MemberId, price.Amount, order.Id, actor, ct);
            }
            catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
            {
                return await BlockAfterCollisionAsync(tx, state.MemberId, key, before, ex);
            }

            if (!fund.IsSuccess)
            {
                // El saldo se miró bajo el candado: no debería pasar. El débito no escribió nada.
                await SafeRollbackAsync(tx, state.MemberId);
                await ForgetAfterRollbackAsync(before);
                return Settled.Blocked(fund.ErrorCode ?? AnnualFeeErrors.InsufficientCommissionBalance);
            }

            payment.Complete(fund.Value!.PaymentHistoryId, now, actor);
            MarkPaid(order, actor, now);
            AdvanceOnSuccess(state, cycle, now, now, actor);
            AddAttempt(state, price.ProductId, RecurringFundingSource.CommissionBalance, RecurringAttemptOutcome.Success,
                price.Amount, order.Id, fund.Value.PaymentHistoryId, tokenTransactionId: null, fund.Value.DebitEarningId,
                failureReason: null, now, actor);

            return await CommitOrBlockAsync(tx, Settled.Paid(payment, order, AnnualFeeMethods.CommissionBalance), key, before, ct);
        }
        catch (Exception)
        {
            // Revisión C-1: cualquier otra excepción bajo el candado (no un choque de índice, que se
            // trata dentro) deshace, suelta lo añadido y relee lo tocado, y se relanza. El contexto
            // lo comparte el trabajador: su siguiente SaveChanges no puede guardar nada a medias.
            await AbortUnderGateAsync(tx, state.MemberId, before);
            throw;
        }
    }

    /// <summary>
    /// Bajo el candado: relee el estado y dice si no se puede cobrar este ciclo. ReplayCode = la clave ya
    /// tiene pago. Pagado, en curso o en revisión = hay un pago vivo para el ciclo. En automático, si el
    /// vencimiento ya está en el futuro es que alguien lo pagó mientras tanto.
    /// </summary>
    private async Task<string?> BlockedUnderGateAsync(
        SubscriptionBillingState state, string? key, DueRule rule, DateTime now, CancellationToken ct)
    {
        await _db.Entry(state).ReloadAsync(ct);

        if (key is not null
            && await _db.AnnualFeePayments.AsNoTracking().AnyAsync(p => p.MemberId == state.MemberId && p.IdempotencyKey == key, ct))
            return ReplayCode;

        var cycle = state.NextBillingDate.Date;
        var live = await AnnualFeePaymentQueries.LiveForCycle(_db, state.Id, cycle).AsNoTracking()
            .Select(p => (AnnualFeePaymentStatus?)p.Status)
            .FirstOrDefaultAsync(ct);

        switch (live)
        {
            case AnnualFeePaymentStatus.Completed:           return AnnualFeeErrors.AlreadyPaid;
            case AnnualFeePaymentStatus.Pending:             return AnnualFeeErrors.InProgress;
            case AnnualFeePaymentStatus.NeedsReconciliation: return AnnualFeeErrors.NeedsReview;
        }

        return rule switch
        {
            DueRule.Due when cycle > now.Date                               => AnnualFeeErrors.AlreadyPaid,
            DueRule.PayAhead when !AnnualFeeSchedule.IsPayable(cycle, now)  => AnnualFeeErrors.NotDue,
            _                                                               => null
        };
    }

    /// <summary>
    /// Guarda y confirma. Si un índice único choca (otra instancia sin candado compartido cobró el mismo
    /// ciclo o usó la misma clave) no se guardó nada: se suelta y se dice por qué.
    /// </summary>
    private async Task<Settled> CommitOrBlockAsync(
        IDbContextTransaction tx, Settled paid, string? key, HashSet<object> before, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return paid;
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        {
            return await BlockAfterCollisionAsync(tx, paid.Payment!.MemberId, key, before, ex);
        }
    }

    private async Task<Settled> BlockAfterCollisionAsync(
        IDbContextTransaction tx, string memberId, string? key, HashSet<object> before, Exception ex)
    {
        await SafeRollbackAsync(tx, memberId);
        await ForgetAfterRollbackAsync(before);
        _logger.LogWarning(ex, "Cuota anual de {MemberId}: el guardado chocó con otro cobro del mismo ciclo o la misma clave.", memberId);
        return Settled.Blocked(key is null ? AnnualFeeErrors.InProgress : ReplayCode);
    }

    /// <summary>Lo que el contexto tiene cargado antes de entrar al candado (para olvidar lo demás si se deshace).</summary>
    private HashSet<object> Tracked() =>
        _db.ChangeTracker.Entries().Select(e => e.Entity).ToHashSet(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Tras deshacer, lo añadido dentro de la transacción se suelta y lo que ya estaba se relee. No se
    /// limpia el contexto entero: lo comparte quien llama (el motor y su trabajador).
    /// </summary>
    private async Task ForgetAfterRollbackAsync(HashSet<object> before)
    {
        foreach (var entry in _db.ChangeTracker.Entries().ToList())
        {
            if (!before.Contains(entry.Entity)) entry.State = EntityState.Detached;
            // Revisión M-2: solo se releen los tipos que escribe el cobro de la cuota; lo demás que tenga
            // cargado quien llama (el trozo del trabajador, el lote) no lo tocó esta transacción.
            else if (entry.State != EntityState.Detached && TouchedTypes.Contains(entry.Metadata.ClrType))
                await entry.ReloadAsync(CancellationToken.None);
        }
    }

    /// <summary>Lo que escribe el cobro de la cuota dentro del candado (y nada más se relee al deshacer).</summary>
    private static readonly HashSet<Type> TouchedTypes =
    [
        typeof(SubscriptionBillingState), typeof(Orders), typeof(AnnualFeePayment), typeof(PaymentHistory),
        typeof(RecurringBillingAttempt), typeof(MLMConquerorGlobalEdition.Domain.Entities.Tokens.TokenTransaction),
        typeof(MLMConquerorGlobalEdition.Domain.Entities.Tokens.TokenBalance),
        typeof(MLMConquerorGlobalEdition.Domain.Entities.Commission.CommissionEarning),
        typeof(MLMConquerorGlobalEdition.Domain.Entities.Commission.DailyResidualEarning)
    ];

    /// <summary>
    /// Deshace la transacción del candado y olvida lo que se escribió dentro, sin que un fallo al olvidar
    /// tape la excepción que se va a relanzar (revisión C-1).
    /// </summary>
    private async Task AbortUnderGateAsync(IDbContextTransaction tx, string memberId, HashSet<object> before)
    {
        await SafeRollbackAsync(tx, memberId);
        try
        {
            await ForgetAfterRollbackAsync(before);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cuota anual de {MemberId}: no se pudo olvidar lo escrito tras deshacer.", memberId);
            foreach (var entry in _db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added).ToList())
                entry.State = EntityState.Detached;
        }
    }

    /// <summary>Deshace sin que un fallo al deshacer (conexión caída) tape lo demás (revisión M6).</summary>
    private async Task SafeRollbackAsync(IDbContextTransaction tx, string memberId)
    {
        try { await tx.RollbackAsync(CancellationToken.None); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cuota anual de {MemberId}: falló al deshacer la transacción del candado.", memberId);
        }
    }

    // ── Sin fondos, decisiones y resultados ──────────────────────────────────

    /// <summary>Intento fallido: la cadencia del plan y, agotada, Stopped (MarkExpired). Ni pedido ni membresía.</summary>
    private async Task<Result<AnnualFeeChargeOutcome>> RecordNoFundsAsync(
        SubscriptionBillingState state, AnnualFeePrice price, MemberProfile member, CancellationToken ct)
    {
        var now = _clock.Now;
        _scheduler.Advance(state, state.Plan!, succeeded: false, attemptDate: now);
        state.LastFailureReason = AnnualFeeErrors.NoFunds;
        state.LastUpdateDate    = now;
        state.LastUpdateBy      = AutoActor;

        AddAttempt(state, price.ProductId,
            member.FeesFromBalance ? RecurringFundingSource.CommissionBalance : RecurringFundingSource.Token,
            RecurringAttemptOutcome.Failed, price.Amount, orderId: null, paymentHistoryId: null,
            tokenTransactionId: null, commissionDebitId: null, AnnualFeeErrors.NoFunds, now, AutoActor);

        await _db.SaveChangesAsync(ct);
        return Result<AnnualFeeChargeOutcome>.Success(
            new AnnualFeeChargeOutcome(AnnualFeeOutcomes.Failed, FailureReason: AnnualFeeErrors.NoFunds));
    }

    /// <summary>Lo que se hace con el estado cuando una exclusión impide cobrar hoy (K5). No es un intento.</summary>
    private static void ApplyDecision(SubscriptionBillingState state, AnnualFeeDecision decision, DateTime now)
    {
        switch (decision.Kind)
        {
            case AnnualFeeDecisionKind.Stop:
                state.Status            = RecurringBillingStatus.Stopped;
                state.LastFailureReason = decision.Reason;
                break;

            case AnnualFeeDecisionKind.Postpone:
                state.NextAttemptDate   = now.Date.AddDays(1);
                state.LastFailureReason = decision.Reason;
                break;

            case AnnualFeeDecisionKind.Reschedule:
                state.NextBillingDate     = decision.NextDue!.Value;
                state.NextAttemptDate     = decision.NextDue.Value;
                state.CurrentAttemptIndex = 0;
                state.Status              = RecurringBillingStatus.Active;
                state.LastFailureReason   = null;
                break;
        }

        state.LastAttemptOutcome = "Skipped";
        state.LastUpdateDate     = now;
        state.LastUpdateBy       = AutoActor;
    }

    private static Result<AnnualFeeChargeOutcome> Outcome(Settled s) => s.Kind switch
    {
        SettleKind.Paid => Result<AnnualFeeChargeOutcome>.Success(new AnnualFeeChargeOutcome(
            AnnualFeeOutcomes.Success, s.FundingSource, s.Order!.Id, s.Payment!.PaymentHistoryId)),
        SettleKind.Review => Result<AnnualFeeChargeOutcome>.Success(new AnnualFeeChargeOutcome(
            AnnualFeeOutcomes.Skipped, s.FundingSource, s.Order?.Id, FailureReason: AnnualFeeErrors.NeedsReview)),
        _ => Skipped(s.Code)
    };

    private static Result<AnnualFeeChargeOutcome> Skipped(string? reason) =>
        Result<AnnualFeeChargeOutcome>.Success(new AnnualFeeChargeOutcome(AnnualFeeOutcomes.Skipped, FailureReason: reason));

    // ── Piezas ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pedido de la cuota: SIN OrderDetails (K9: no suma puntos, no cuenta como pedido de membresía para
    /// el nivel por la orden, no dispara comisiones) y SIN el bucle de unicidad de OrderNo (K20).
    /// </summary>
    private Orders NewOrder(string memberId, decimal total, DateTime cycle, string actor, DateTime now)
    {
        var order = new Orders
        {
            MemberId       = memberId,
            OrderNo        = OrderNumberHelper.Generate("Annual Fee", now),
            TotalAmount    = total,
            Status         = OrderStatus.Processing,
            OrderDate      = now,
            Notes          = $"Biz Center annual fee — cycle {cycle:yyyy-MM-dd}",
            CreationDate   = now,
            CreatedBy      = actor,
            LastUpdateDate = now,
            LastUpdateBy   = actor
        };
        _db.Orders.Add(order);
        return order;
    }

    private AnnualFeePayment NewPayment(
        SubscriptionBillingState state, DateTime cycle, decimal amount, AnnualFeePaymentMethod method,
        AnnualFeePaymentSource source, string? key, Orders order, string actor, DateTime now)
    {
        var payment = new AnnualFeePayment
        {
            MemberId                   = state.MemberId,
            SubscriptionBillingStateId = state.Id,
            CycleDueDate               = cycle,
            Amount                     = amount,
            Method                     = method,
            Source                     = source,
            Status                     = AnnualFeePaymentStatus.Pending,
            OrderId                    = order.Id,
            IdempotencyKey             = key,
            CreatedBy                  = actor,
            CreationDate               = now,
            LastUpdateDate             = now,
            LastUpdateBy               = actor
        };
        _db.AnnualFeePayments.Add(payment);
        return payment;
    }

    private static void MarkPaid(Orders order, string actor, DateTime now)
    {
        order.Status         = OrderStatus.Paid;
        order.LastUpdateDate = now;
        order.LastUpdateBy   = actor;
    }

    private static void Cancel(Orders order, string actor, DateTime now)
    {
        order.Status         = OrderStatus.Cancelled;
        order.LastUpdateDate = now;
        order.LastUpdateBy   = actor;
    }

    /// <summary>
    /// Cobrado el ciclo <paramref name="cycle"/> en <paramref name="paidAt"/>: el siguiente según K4
    /// (revisado por Nick) y el ancla según la revisión M3: a tiempo se queda; tarde pasa a la fecha de pago.
    /// </summary>
    private static void AdvanceOnSuccess(SubscriptionBillingState state, DateTime cycle, DateTime paidAt, DateTime now, string actor)
    {
        var next = AnnualFeeSchedule.NextDueAfterPayment(cycle, paidAt, state.BillingAnchorDate);
        state.BillingAnchorDate         = AnnualFeeSchedule.AnchorAfterPayment(cycle, paidAt, state.BillingAnchorDate);
        state.LastSuccessfulBillingDate = paidAt;
        state.NextBillingDate           = next;
        state.NextAttemptDate           = next;
        state.CurrentAttemptIndex       = 0;
        state.Status                    = RecurringBillingStatus.Active;
        state.LastAttemptAt             = paidAt;
        state.LastAttemptOutcome        = "Success";
        state.LastFailureReason         = null;
        state.LastUpdateDate            = now;
        state.LastUpdateBy              = actor;
    }

    private void AddAttempt(
        SubscriptionBillingState state, string productId, RecurringFundingSource source, RecurringAttemptOutcome outcome,
        decimal amount, string? orderId, string? paymentHistoryId, long? tokenTransactionId, string? commissionDebitId,
        string? failureReason, DateTime now, string actor)
    {
        _db.RecurringBillingAttempts.Add(new RecurringBillingAttempt
        {
            SubscriptionBillingStateId   = state.Id,
            MemberId                     = state.MemberId,
            ProductId                    = productId,
            AttemptIndex                 = state.CurrentAttemptIndex,
            AttemptedAt                  = now,
            Amount                       = amount,
            FundingSource                = source,
            Outcome                      = outcome,
            PaymentHistoryId             = paymentHistoryId,
            OrderId                      = orderId,
            TokenTransactionId           = tokenTransactionId,
            CommissionDeductionEarningId = commissionDebitId,
            FailureReason                = failureReason,
            CreatedBy                    = actor,
            CreationDate                 = now
        });
    }
}
