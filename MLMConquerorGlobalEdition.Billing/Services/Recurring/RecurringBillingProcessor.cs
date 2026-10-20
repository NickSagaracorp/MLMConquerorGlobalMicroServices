using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Billing;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Services.Recurring;

/// <summary>
/// Orchestrates one recurring billing attempt end-to-end.
/// This is the heart of the dunning engine.
/// </summary>
public class RecurringBillingProcessor : IRecurringBillingProcessor
{
    private readonly AppDbContext _db;
    private readonly ICommissionBalanceService _commissionBalance;
    private readonly IRecurringBillingScheduler _scheduler;
    private readonly IGatewayRouter _router;
    private readonly IGatewayChargeOrchestrator _orchestrator;
    private readonly ICardBrandDetector _brandDetector;
    private readonly IMemberFundsGate _gate;
    private readonly IAnnualFeeChargeService _annualFee;
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<RecurringBillingProcessor> _logger;
    private readonly IConfiguration? _configuration;

    private const string Actor = "recurring-billing-engine";

    /// <summary>
    /// El candado de fondos del miembro estaba tomado (un pago, una compra de tokens, otra renovación):
    /// no se movió nada, el pedido se canceló y el estado sigue igual para la siguiente pasada.
    /// </summary>
    public const string MemberFundsBusyCode = "MEMBER_FUNDS_BUSY";

    /// <summary>
    /// El estado y su plan no coinciden: un estado de membresía en el plan de la cuota, o uno de cuota en
    /// otro plan. No se cobra nada; hay que mirarlo a mano (Plan K, K2).
    /// </summary>
    public const string AnnualFeeStateMismatchCode = "ANNUAL_FEE_STATE_MISMATCH";

    /// <summary>La membresía está cancelada o con la cancelación cumplida: el estado se para sin cobrar.</summary>
    public const string ReasonMembershipCancelled = MembershipBillingStops.ReasonMembershipCancelled;

    /// <summary>La membresía es gratuita: el estado se para sin cobrar.</summary>
    public const string ReasonMembershipFree = MembershipBillingStops.ReasonMembershipFree;

    /// <summary>Otra suscripción reemplazó a la del estado (subida o bajada): el estado se para sin cobrar.</summary>
    public const string ReasonMembershipReplaced = MembershipBillingStops.ReasonMembershipReplaced;

    /// <summary>La suscripción del estado está borrada o no existe: el estado se para (revisión M-9).</summary>
    public const string ReasonSubscriptionMissing = MembershipBillingStops.ReasonSubscriptionMissing;

    public RecurringBillingProcessor(
        AppDbContext db,
        ICommissionBalanceService commissionBalance,
        IRecurringBillingScheduler scheduler,
        IGatewayRouter router,
        IGatewayChargeOrchestrator orchestrator,
        ICardBrandDetector brandDetector,
        IMemberFundsGate gate,
        IAnnualFeeChargeService annualFee,
        IDateTimeProvider dateTime,
        ILogger<RecurringBillingProcessor> logger,
        IConfiguration? configuration = null)
    {
        _db               = db;
        _commissionBalance = commissionBalance;
        _scheduler        = scheduler;
        _router           = router;
        _orchestrator     = orchestrator;
        _brandDetector    = brandDetector;
        _gate             = gate;
        _annualFee        = annualFee;
        _dateTime         = dateTime;
        _logger           = logger;
        _configuration    = configuration;
    }

    public async Task<Result<RecurringBillingProcessorResult>> ProcessAsync(
        string billingStateId,
        bool forceBillNow = false,
        CancellationToken ct = default)
    {
        var now = _dateTime.Now;

        // 1. Load state + plan
        var state = await _db.SubscriptionBillingStates
            .Include(s => s.Plan)
                .ThenInclude(p => p!.PlanProducts)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == billingStateId, ct);

        if (state is null)
            return Result<RecurringBillingProcessorResult>.Failure("STATE_NOT_FOUND",
                $"SubscriptionBillingState '{billingStateId}' not found.");

        var plan = state.Plan!;

        // Plan K (K1, K2): un estado de cuota anual lo cobra AnnualFeeChargeService. Mismo motor
        // (planificador, trabajadores, barrido) y otro cobro: no hay suscripción que renovar. Va antes de
        // la comprobación de Stopped porque el servicio de la cuota la hace a su manera (y revive si se fuerza).
        if (state.Purpose == BillingStatePurpose.AnnualFee || plan.IsAnnualFeePlan)
            return await DelegateAnnualFeeAsync(state, plan, forceBillNow, ct);

        // 2. If Stopped and not force-billing → skip
        if (state.Status == RecurringBillingStatus.Stopped && !forceBillNow)
        {
            return Result<RecurringBillingProcessorResult>.Success(new RecurringBillingProcessorResult
            {
                BillingStateId = billingStateId,
                Outcome        = "Skipped",
                FailureReason  = "Billing state is Stopped."
            });
        }

        // Plan K (revisión, C2): una membresía cancelada, con la cancelación cumplida o gratuita no se
        // cobra, tampoco forzado. Se para el estado sin pedido ni intento y sin tocar la membresía (si no,
        // el tope de días sin cobrar le pondría HoldByBilling o Expired encima de la cancelación).
        var subscriptionToCheck = await _db.MembershipSubscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == state.MembershipSubscriptionId && !s.IsDeleted, ct);
        // Revisión M-9: sin suscripción (borrada o inexistente) no hay nada que renovar; se para con su
        // motivo en vez de fallar SUBSCRIPTION_NOT_FOUND en cada pasada.
        var notBillable = subscriptionToCheck is null
            ? MembershipBillingStops.ReasonSubscriptionMissing
            : MembershipBillingStops.WhyNotBillable(subscriptionToCheck, now)
              // Plan Q (tercera revisión del bloque 2, C-a): la reemplazada por una subida o bajada no se cobra.
              ?? (await MembershipBillingStops.IsReplacedAsync(_db, subscriptionToCheck, ct)
                  ? MembershipBillingStops.ReasonMembershipReplaced : null);
        if (notBillable is not null)
        {
            MembershipBillingStops.Stop(state, notBillable, now, Actor);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Renovación {StateId} de {MemberId}: estado parado sin cobrar ({Reason}).",
                state.Id, state.MemberId, notBillable);
            return Result<RecurringBillingProcessorResult>.Success(new RecurringBillingProcessorResult
            {
                BillingStateId = billingStateId,
                Outcome        = "Skipped",
                FailureReason  = notBillable
            });
        }

        // When forcing a bill: revive Stopped state
        if (state.Status == RecurringBillingStatus.Stopped && forceBillNow)
        {
            state.Status              = RecurringBillingStatus.Active;
            state.CurrentAttemptIndex = 0;
            state.NextAttemptDate     = now.Date;
        }

        // 3. Check StopAfterUnbilledDays BEFORE processing (§5 — pre-attempt stop check)
        if (plan.StopAfterUnbilledDays.HasValue && !forceBillNow)
        {
            var referenceDate  = MembershipBillingStops.UnbilledSince(state);
            var unbilledDays   = (now.Date - referenceDate.Date).TotalDays;
            if (unbilledDays >= plan.StopAfterUnbilledDays.Value)
            {
                state.Status        = RecurringBillingStatus.Stopped;
                state.LastUpdateDate = now;
                state.LastUpdateBy   = Actor;

                // El estado terminal de la membresía va en el MISMO guardado que la parada: antes se
                // aplicaba después del SaveChanges y solo se persistía si el contexto volvía a guardar.
                await ApplyTerminalMembershipStatusAsync(state, plan, ct);
                await _db.SaveChangesAsync(ct);

                return Result<RecurringBillingProcessorResult>.Success(new RecurringBillingProcessorResult
                {
                    BillingStateId = billingStateId,
                    Outcome        = "Skipped",
                    FailureReason  = $"Stop threshold of {plan.StopAfterUnbilledDays} days reached."
                });
            }
        }

        // 4. Load subscription + determine product + amount
        var subscription = await _db.MembershipSubscriptions
            .Include(s => s.MembershipLevel)
            .FirstOrDefaultAsync(s => s.Id == state.MembershipSubscriptionId && !s.IsDeleted, ct);

        if (subscription is null)
            return Result<RecurringBillingProcessorResult>.Failure("SUBSCRIPTION_NOT_FOUND",
                $"Subscription '{state.MembershipSubscriptionId}' not found.");

        // Determine the product for this subscription
        var productId = await ResolveProductIdAsync(subscription, ct);
        if (productId is null)
            return Result<RecurringBillingProcessorResult>.Failure("PRODUCT_NOT_FOUND",
                "No product linked to this subscription's membership level.");

        var planProduct = plan.PlanProducts.FirstOrDefault(pp => pp.ProductId == productId);

        var amountDue = await ResolveAmountAsync(plan, productId, ct);
        if (amountDue <= 0)
            return Result<RecurringBillingProcessorResult>.Failure("INVALID_AMOUNT",
                $"Resolved billing amount is {amountDue} for product '{productId}'.");

        // 5. Create the renewal Order (Processing)
        var order = await CreateRenewalOrderAsync(state, subscription, productId, amountDue, now, ct);

        // 6. Commission-balance first? Bajo el candado de fondos del miembro (Plan K, K18): sin candado,
        //    un pago (PayoutOrchestrator) o una compra de tokens podía gastar las mismas comisiones a la
        //    vez. El saldo se lee DENTRO del candado y es el GASTABLE (GetSpendableAsync, aprobado por
        //    Nick, K18/R7): sin las comisiones reservadas por un pago en curso ni las aún no pagables.
        if (plan.PayFromCommissionBalanceFirst)
        {
            var funded = await TryCommissionFundedAsync(
                state, plan, planProduct, subscription, order, productId, amountDue, now, ct);
            if (funded is not null) return funded;
        }

        // 7. Fall through to card charge
        return await HandleCardChargeAsync(
            state, plan, subscription, order, productId, amountDue, now, ct);
    }

    /// <summary>
    /// Paga con comisiones bajo el candado del miembro. Null = el saldo gastable no alcanza y se pasa a la
    /// tarjeta. Ocupado = se cancela el pedido y no se toca el estado. Si algo falla dentro, se deshace
    /// entero (también el débito, que FundWithCommissionBalanceAsync escribe antes de comprobar el tipo
    /// de token) y el pedido se cancela después.
    /// </summary>
    private async Task<Result<RecurringBillingProcessorResult>?> TryCommissionFundedAsync(
        SubscriptionBillingState state,
        RecurringBillingPlan plan,
        RecurringBillingPlanProduct? planProduct,
        MembershipSubscription subscription,
        Orders order,
        string productId,
        decimal amountDue,
        DateTime now,
        CancellationToken ct)
    {
        // Lo que el contexto tiene cargado antes de entrar está guardado (el pedido se acaba de crear):
        // si hay que deshacer, eso se relee y lo nuevo se suelta. No se limpia el contexto entero porque
        // lo comparte quien llama (el trabajador guarda su trozo al final).
        var before = _db.ChangeTracker.Entries().Select(e => e.Entity).ToHashSet(ReferenceEqualityComparer.Instance);

        // Espera del candado (revisión M6): SqlServerMemberFundsGate espera hasta 15 s. En el trabajador va
        // en serie, así que un miembro ocupado retrasa su trozo como mucho 15 s. Es aceptable: el candado
        // solo lo toman a la vez un pago, una compra de tokens o la cuota de ESE miembro, que de madrugada
        // es raro; y lo ocupado no se pierde, se reintenta en la siguiente pasada (MEMBER_FUNDS_BUSY).
        IDbContextTransaction tx;
        try
        {
            tx = await _gate.EnterAsync(state.MemberId, ct);
        }
        catch (MemberFundsBusyException)
        {
            _logger.LogWarning(
                "Renovación {StateId} de {MemberId}: el candado de fondos está ocupado; se reintenta en la siguiente pasada.",
                state.Id, state.MemberId);
            // Sin el token de la llamada: el pedido ya está creado y tiene que quedar cancelado aunque
            // se esté cancelando la pasada (revisión M6).
            await CancelOrderAsync(order, now, CancellationToken.None);
            return Result<RecurringBillingProcessorResult>.Failure(MemberFundsBusyCode,
                $"Funds of member {state.MemberId} are busy; the renewal will be retried on the next pass.");
        }

        await using (tx)
        {
            var spendable = await _commissionBalance.GetSpendableAsync(state.MemberId, ct);
            if (spendable < amountDue)
            {
                await SafeRollbackAsync(tx, state);
                return null;
            }

            try
            {
                var result = await HandleCommissionFundedAsync(
                    state, plan, planProduct, subscription, order, productId, amountDue, now, ct);

                if (result.IsSuccess)
                {
                    await tx.CommitAsync(ct);
                    return result;
                }

                await SafeRollbackAsync(tx, state);
                await ForgetRolledBackAsync(before);
                await CancelOrderAfterRollbackAsync(order, now);
                return result;
            }
            catch (Exception original)
            {
                await SafeRollbackAsync(tx, state);
                try
                {
                    await ForgetRolledBackAsync(before);
                    await CancelOrderAfterRollbackAsync(order, now);
                }
                catch (Exception cleanup)
                {
                    // Que la limpieza no tape el error de verdad. Lo deshecho, deshecho está; un pedido
                    // que quede en Processing no cobra nada.
                    _logger.LogError(cleanup,
                        "Renovación {StateId} de {MemberId}: no se pudo cancelar el pedido {OrderId} tras deshacer ({Error}).",
                        state.Id, state.MemberId, order.Id, original.Message);
                }
                throw;
            }
        }
    }

    /// <summary>
    /// Deshace sin que un fallo al deshacer (conexión caída) tape lo demás (revisión M6). Si la
    /// conexión se cayó, SQL Server ya deshizo la transacción al cerrar la sesión; se registra y sigue.
    /// </summary>
    private async Task SafeRollbackAsync(IDbContextTransaction tx, SubscriptionBillingState state)
    {
        try
        {
            await tx.RollbackAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Renovación {StateId} de {MemberId}: falló al deshacer la transacción del candado.",
                state.Id, state.MemberId);
        }
    }

    /// <summary>
    /// Tras deshacer, lo que el contexto creía guardado dentro de la transacción ya no lo está: lo que
    /// se añadió se suelta y lo que ya estaba se relee de la base.
    /// </summary>
    private async Task ForgetRolledBackAsync(HashSet<object> before)
    {
        foreach (var entry in _db.ChangeTracker.Entries().ToList())
        {
            if (!before.Contains(entry.Entity))
                entry.State = EntityState.Detached;
            // Revisión M-2: solo los tipos que escribe la renovación con comisiones.
            else if (RenewalTouchedTypes.Contains(entry.Metadata.ClrType))
                await entry.ReloadAsync(CancellationToken.None);
        }
    }

    private static readonly HashSet<Type> RenewalTouchedTypes =
    [
        typeof(SubscriptionBillingState), typeof(MembershipSubscription), typeof(Orders), typeof(OrderDetail),
        typeof(PaymentHistory), typeof(RecurringBillingAttempt),
        typeof(MLMConquerorGlobalEdition.Domain.Entities.Tokens.TokenTransaction),
        typeof(MLMConquerorGlobalEdition.Domain.Entities.Tokens.TokenBalance),
        typeof(MLMConquerorGlobalEdition.Domain.Entities.Commission.CommissionEarning),
        typeof(MLMConquerorGlobalEdition.Domain.Entities.Commission.DailyResidualEarning)
    ];

    /// <summary>Cancela el pedido (creado y guardado antes del candado) si sigue en Processing.</summary>
    private async Task CancelOrderAfterRollbackAsync(Orders order, DateTime now)
    {
        if (_db.Entry(order).State == EntityState.Detached || order.Status != OrderStatus.Processing) return;

        order.Status         = OrderStatus.Cancelled;
        order.LastUpdateDate = now;
        order.LastUpdateBy   = Actor;
        await _db.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<Result<RecurringBillingProcessorResult>> DelegateAnnualFeeAsync(
        SubscriptionBillingState state, RecurringBillingPlan plan, bool forceBillNow, CancellationToken ct)
    {
        if (state.Purpose != BillingStatePurpose.AnnualFee || !plan.IsAnnualFeePlan)
        {
            _logger.LogError(
                "Estado {StateId} de {MemberId}: propósito {Purpose} en el plan {PlanId} (IsAnnualFeePlan={IsAnnual}). No se cobra.",
                state.Id, state.MemberId, state.Purpose, plan.Id, plan.IsAnnualFeePlan);
            return Result<RecurringBillingProcessorResult>.Failure(AnnualFeeStateMismatchCode,
                "The billing state purpose does not match its plan.");
        }

        var charged = await _annualFee.ChargeDueAsync(state.Id, forceBillNow, ct);
        if (!charged.IsSuccess)
            return Result<RecurringBillingProcessorResult>.Failure(charged.ErrorCode!, charged.Error!);

        var o = charged.Value!;
        return Result<RecurringBillingProcessorResult>.Success(new RecurringBillingProcessorResult
        {
            BillingStateId   = state.Id,
            Outcome          = o.Outcome,
            FundingSource    = o.FundingSource,
            PaymentHistoryId = o.PaymentHistoryId,
            OrderId          = o.OrderId,
            FailureReason    = o.FailureReason
        });
    }

    // ── Commission-balance path ───────────────────────────────────────────────

    private async Task<Result<RecurringBillingProcessorResult>> HandleCommissionFundedAsync(
        SubscriptionBillingState state,
        RecurringBillingPlan plan,
        RecurringBillingPlanProduct? planProduct,
        MembershipSubscription subscription,
        Orders order,
        string productId,
        decimal amountDue,
        DateTime now,
        CancellationToken ct)
    {
        var fundResult = await _commissionBalance.FundWithCommissionBalanceAsync(
            state.MemberId,
            plan,
            planProduct?.TokenTypeIdOverride,
            amountDue,
            order.Id,
            productId,
            Actor,
            ct);

        if (!fundResult.IsSuccess)
        {
            // Commission funding failed (shouldn't happen — balance was checked) — cancel order
            order.Status         = OrderStatus.Cancelled;
            order.LastUpdateDate = now;
            order.LastUpdateBy   = Actor;
            await _db.SaveChangesAsync(ct);

            return Result<RecurringBillingProcessorResult>.Failure(
                fundResult.ErrorCode!, fundResult.Error!);
        }

        var fund = fundResult.Value!;

        // Advance state on success
        // Revisión M-5: el número de este intento en el ciclo (1 = el primero), como en los fallos.
        var attemptIndex = state.CurrentAttemptIndex + 1;
        _scheduler.Advance(state, plan, succeeded: true, attemptDate: now);
        state.LastUpdateDate = now;
        state.LastUpdateBy   = Actor;

        // Update subscription
        UpdateSubscription(subscription, order.Id, now);

        // Record attempt
        var attempt = new RecurringBillingAttempt
        {
            SubscriptionBillingStateId   = state.Id,
            MemberId                     = state.MemberId,
            ProductId                    = productId,
            AttemptIndex                 = attemptIndex,
            AttemptedAt                  = now,
            Amount                       = amountDue,
            FundingSource                = RecurringFundingSource.CommissionBalance,
            Outcome                      = RecurringAttemptOutcome.Success,
            PaymentHistoryId             = fund.PaymentHistoryId,
            OrderId                      = order.Id,
            TokenTransactionId           = fund.TokenTransactionId,
            CommissionDeductionEarningId = fund.CommissionDebitEarningId,
            CreatedBy                    = Actor,
            CreationDate                 = now
        };
        _db.RecurringBillingAttempts.Add(attempt);

        // Lift any terminal membership status on success
        if (subscription.SubscriptionStatus is MembershipStatus.HoldByBilling or MembershipStatus.Expired)
            await LiftTerminalMembershipStatusAsync(state.MembershipSubscriptionId, now, ct);

        await _db.SaveChangesAsync(ct);

        return Result<RecurringBillingProcessorResult>.Success(new RecurringBillingProcessorResult
        {
            BillingStateId  = state.Id,
            Outcome         = "Success",
            FundingSource   = "CommissionBalance",
            PaymentHistoryId = fund.PaymentHistoryId,
            OrderId          = order.Id
        });
    }

    // ── Card charge path ──────────────────────────────────────────────────────

    private async Task<Result<RecurringBillingProcessorResult>> HandleCardChargeAsync(
        SubscriptionBillingState state,
        RecurringBillingPlan plan,
        MembershipSubscription subscription,
        Orders order,
        string productId,
        decimal amountDue,
        DateTime now,
        CancellationToken ct)
    {
        // Plan K (revisión I1): con la tarjeta apagada (por defecto, mientras el gateway sea un stub) ni
        // se llama al gateway: sin PaymentHistory simulado. Es un fallo normal: intento Failed y cadencia.
        if (!RecurringCardSwitch.IsEnabled(_configuration))
            return await RecordCardFailureAsync(state, plan, order, productId, amountDue, now,
                RecurringCardSwitch.CardDisabledCode,
                "Recurring card billing is disabled until the card gateway is real (Plan I).", ct);

        // Determine cardholder country
        var memberProfile = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == state.MemberId, ct);

        var country = memberProfile?.Country?.Length == 2
            ? memberProfile.Country.ToUpperInvariant()
            : "US";

        // Detect card brand
        var creditCard = await _db.CreditCards
            .AsNoTracking()
            .Where(c => c.MemberId == state.MemberId && c.IsDefault && !c.IsDeleted)
            .OrderBy(c => c.Priority)
            .FirstOrDefaultAsync(ct);

        var cardBrand = creditCard is not null
            ? _brandDetector.Detect(creditCard.First6)
            : CardBrand.Visa;

        var routingCtx = new GatewayRoutingContext
        {
            OperationType        = BillingOperationType.Payment,
            CardBrand            = cardBrand,
            CardholderCountryIso = country,
            AmountUsd            = amountDue,
            MemberId             = state.MemberId
        };

        var planResult = await _router.ResolveAsync(routingCtx, ct);
        if (!planResult.IsSuccess)
        {
            await CancelOrderAsync(order, now, ct);
            return Result<RecurringBillingProcessorResult>.Failure(
                planResult.ErrorCode!, planResult.Error!);
        }

        var chargeReq = new OrchestratorChargeRequest
        {
            MemberId             = state.MemberId,
            TokenizedCardRef     = creditCard?.CardToken,
            NetworkTransactionId = creditCard?.GatewayToken,
            Description          = $"Recurring billing — {plan.Name}",
            OrderId              = order.Id,
            IsRecurring          = true
        };

        var chargeResult = await _orchestrator.ExecuteAsync(planResult.Value!, routingCtx, chargeReq, ct);

        if (!chargeResult.IsSuccess)
            return await RecordCardFailureAsync(state, plan, order, productId, amountDue, now, null, chargeResult.Error, ct);

        var outcome = chargeResult.Value!;

        if (outcome.Status == "Scheduled")
        {
            // 60-min delayed fallback — leave state untouched; the delayed job will update it
            var scheduledAttempt = new RecurringBillingAttempt
            {
                SubscriptionBillingStateId = state.Id,
                MemberId                   = state.MemberId,
                ProductId                  = productId,
                AttemptIndex               = state.CurrentAttemptIndex,
                AttemptedAt                = now,
                Amount                     = amountDue,
                FundingSource              = RecurringFundingSource.CreditCard,
                Outcome                    = RecurringAttemptOutcome.Scheduled,
                OrderId                    = order.Id,
                CreatedBy                  = Actor,
                CreationDate               = now
            };
            _db.RecurringBillingAttempts.Add(scheduledAttempt);
            await _db.SaveChangesAsync(ct);

            return Result<RecurringBillingProcessorResult>.Success(new RecurringBillingProcessorResult
            {
                BillingStateId  = state.Id,
                Outcome         = "Scheduled",
                FundingSource   = "CreditCard",
                PaymentHistoryId = outcome.PaymentHistoryId
            });
        }

        // Plan K (regla de Nick: nunca se cobra con el stub; revisión I1). Con la tarjeta encendida por
        // error, el gateway de Spreedly devuelve un "éxito" simulado. Eso no es dinero: es un fallo normal
        // (intento Failed y cadencia), sin renovar. El PaymentHistory que el orquestador ya escribió queda
        // (es suyo): §13 da la consulta para encontrarlos.
        if (CardChargeOutcomes.IsSimulated(outcome.GatewayTransactionId))
        {
            _logger.LogCritical(
                "Renovación {StateId} de {MemberId}: la tarjeta devolvió un cobro simulado ({TxId}). No se renueva; " +
                "el cobro con tarjeta del motor no puede darse por bueno mientras el gateway sea un stub.",
                state.Id, state.MemberId, outcome.GatewayTransactionId);
            return await RecordCardFailureAsync(state, plan, order, productId, amountDue, now,
                TokenPurchaseErrors.CardSimulatedCharge,
                $"The card gateway returned a simulated charge ({outcome.GatewayTransactionId}); the renewal was not applied.", ct);
        }

        // Immediate success
        order.Status         = OrderStatus.Paid;
        order.LastUpdateDate = now;
        order.LastUpdateBy   = Actor;

        // Revisión M-5: el número de este intento en el ciclo (1 = el primero), como en los fallos.
        var attemptIndex = state.CurrentAttemptIndex + 1;
        _scheduler.Advance(state, plan, succeeded: true, attemptDate: now);
        state.LastUpdateDate = now;
        state.LastUpdateBy   = Actor;

        UpdateSubscription(subscription, order.Id, now);

        var successAttempt = new RecurringBillingAttempt
        {
            SubscriptionBillingStateId = state.Id,
            MemberId                   = state.MemberId,
            ProductId                  = productId,
            AttemptIndex               = attemptIndex,
            AttemptedAt                = now,
            Amount                     = amountDue,
            FundingSource              = RecurringFundingSource.CreditCard,
            Outcome                    = RecurringAttemptOutcome.Success,
            PaymentHistoryId           = outcome.PaymentHistoryId,
            OrderId                    = order.Id,
            CreatedBy                  = Actor,
            CreationDate               = now
        };
        _db.RecurringBillingAttempts.Add(successAttempt);

        if (subscription.SubscriptionStatus is MembershipStatus.HoldByBilling or MembershipStatus.Expired)
            await LiftTerminalMembershipStatusAsync(state.MembershipSubscriptionId, now, ct);

        await _db.SaveChangesAsync(ct);

        return Result<RecurringBillingProcessorResult>.Success(new RecurringBillingProcessorResult
        {
            BillingStateId   = state.Id,
            Outcome          = "Success",
            FundingSource    = "CreditCard",
            PaymentHistoryId = outcome.PaymentHistoryId,
            OrderId          = order.Id
        });
    }

    /// <summary>
    /// Un intento con tarjeta que no cobró: cancela el pedido, avanza la cadencia del plan como fallo,
    /// deja el motivo en el estado y en el intento (con el código delante si lo hay) y, si la cadencia
    /// se agota, aplica el estado terminal de la membresía.
    /// </summary>
    private async Task<Result<RecurringBillingProcessorResult>> RecordCardFailureAsync(
        SubscriptionBillingState state,
        RecurringBillingPlan plan,
        Orders order,
        string productId,
        decimal amountDue,
        DateTime now,
        string? code,
        string? error,
        CancellationToken ct)
    {
        var reason = code is null ? error : $"{code}: {error}";
        if (reason is { Length: > 500 }) reason = reason[..500];

        order.Status         = OrderStatus.Cancelled;
        order.LastUpdateDate = now;
        order.LastUpdateBy   = Actor;

        _scheduler.Advance(state, plan, succeeded: false, attemptDate: now);
        state.LastFailureReason = reason;
        state.LastUpdateDate    = now;
        state.LastUpdateBy      = Actor;

        _db.RecurringBillingAttempts.Add(new RecurringBillingAttempt
        {
            SubscriptionBillingStateId = state.Id,
            MemberId                   = state.MemberId,
            ProductId                  = productId,
            AttemptIndex               = state.CurrentAttemptIndex,
            AttemptedAt                = now,
            Amount                     = amountDue,
            FundingSource              = RecurringFundingSource.CreditCard,
            Outcome                    = RecurringAttemptOutcome.Failed,
            OrderId                    = order.Id,
            FailureReason              = reason,
            CreatedBy                  = Actor,
            CreationDate               = now
        });

        if (state.Status == RecurringBillingStatus.Stopped)
            await ApplyTerminalMembershipStatusAsync(state, plan, ct);

        await _db.SaveChangesAsync(ct);

        return Result<RecurringBillingProcessorResult>.Success(new RecurringBillingProcessorResult
        {
            BillingStateId = state.Id,
            Outcome        = "Failed",
            FundingSource  = "CreditCard",
            FailureReason  = reason
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Orders> CreateRenewalOrderAsync(
        SubscriptionBillingState state,
        MembershipSubscription subscription,
        string productId,
        decimal amountDue,
        DateTime now,
        CancellationToken ct)
    {
        var levelName = subscription.MembershipLevel?.Name ?? "Membership";
        string orderNo;
        do { orderNo = OrderNumberHelper.Generate(levelName, now); }
        while (await _db.Orders.AnyAsync(o => o.OrderNo == orderNo, ct));

        var order = new Orders
        {
            MemberId                 = state.MemberId,
            MembershipSubscriptionId = subscription.Id,
            OrderNo                  = orderNo,
            TotalAmount              = amountDue,
            Status                   = OrderStatus.Processing,
            OrderDate                = now,
            Notes                    = $"Recurring billing — Plan: {state.Plan?.Name} — Attempt {state.CurrentAttemptIndex + 1}",
            CreatedBy                = Actor,
            CreationDate             = now,
            LastUpdateDate           = now,
            LastUpdateBy             = Actor
        };

        var detail = new OrderDetail
        {
            ProductId    = productId,
            Quantity     = 1,
            UnitPrice    = amountDue,
            CreatedBy    = Actor,
            CreationDate = now
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct); // save to get order.Id

        detail.OrderId = order.Id;
        _db.OrderDetails.Add(detail);
        await _db.SaveChangesAsync(ct);

        return order;
    }

    private static void UpdateSubscription(MembershipSubscription subscription, string orderId, DateTime now)
    {
        subscription.StartDate      = now;
        subscription.ChangeReason   = SubscriptionChangeReason.Renewal;
        subscription.LastOrderId    = orderId;
        subscription.LastUpdateDate = now;
        subscription.LastUpdateBy   = Actor;
    }

    private async Task ApplyTerminalMembershipStatusAsync(
        SubscriptionBillingState state,
        RecurringBillingPlan plan,
        CancellationToken ct)
    {
        // Update the subscription status to the appropriate terminal state
        var subscription = await _db.MembershipSubscriptions
            .FirstOrDefaultAsync(s => s.Id == state.MembershipSubscriptionId && !s.IsDeleted, ct);

        if (subscription is not null)
        {
            subscription.SubscriptionStatus = plan.OnAllRetriesFail == RecurringFailurePolicy.MarkExpired
                ? MembershipStatus.Expired
                : MembershipStatus.HoldByBilling;
            subscription.LastUpdateDate = _dateTime.Now;
            subscription.LastUpdateBy   = Actor;
        }
    }

    /// <summary>
    /// Plan Q (revision 2 del bloque 2, I5): reactiva la suscripcion DEL ESTADO que se acaba de cobrar. Antes tomaba
    /// cualquiera del miembro retenida o caducada, sin orden: tras una subida podia reactivar la vieja caducada. Y nunca una
    /// reemplazada (tercera revision, C-a).
    /// </summary>
    private async Task LiftTerminalMembershipStatusAsync(string membershipSubscriptionId, DateTime now, CancellationToken ct)
    {
        var subscription = await _db.MembershipSubscriptions
            .Where(s => s.Id == membershipSubscriptionId
                     && (s.SubscriptionStatus == MembershipStatus.HoldByBilling
                         || s.SubscriptionStatus == MembershipStatus.Expired)
                     && !s.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (subscription is null) return;
        // Plan Q (tercera revisión del bloque 2, C-a): nunca reactiva una reemplazada (dos activas, los puntos dos veces).
        if (await MembershipBillingStops.IsReplacedAsync(_db, subscription, ct))
        {
            _logger.LogWarning("Suscripción {SubscriptionId} reemplazada por una subida o bajada: no se reactiva.",
                membershipSubscriptionId);
            return;
        }

        subscription.SubscriptionStatus = MembershipStatus.Active;
        subscription.LastUpdateDate     = now;
        subscription.LastUpdateBy       = Actor;
    }

    private async Task CancelOrderAsync(Orders order, DateTime now, CancellationToken ct)
    {
        order.Status         = OrderStatus.Cancelled;
        order.LastUpdateDate = now;
        order.LastUpdateBy   = Actor;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<string?> ResolveProductIdAsync(MembershipSubscription subscription, CancellationToken ct)
    {
        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.MembershipLevelId == subscription.MembershipLevelId && !p.IsDeleted, ct);
        return product?.Id;
    }

    private async Task<decimal> ResolveAmountAsync(
        RecurringBillingPlan plan, string productId, CancellationToken ct)
    {
        if (plan.FixedAmountOverride.HasValue)
            return plan.FixedAmountOverride.Value;

        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId, ct);

        if (product is null) return 0m;

        return plan.CycleType == RecurringCycleType.Every30Days
            ? product.MonthlyFee
            : product.AnnualPrice;
    }
}
