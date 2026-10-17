using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
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
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<RecurringBillingProcessor> _logger;

    private const string Actor = "recurring-billing-engine";

    /// <summary>
    /// El candado de fondos del miembro estaba tomado (un pago, una compra de tokens, otra renovación):
    /// no se movió nada, el pedido se canceló y el estado sigue igual para la siguiente pasada.
    /// </summary>
    public const string MemberFundsBusyCode = "MEMBER_FUNDS_BUSY";

    public RecurringBillingProcessor(
        AppDbContext db,
        ICommissionBalanceService commissionBalance,
        IRecurringBillingScheduler scheduler,
        IGatewayRouter router,
        IGatewayChargeOrchestrator orchestrator,
        ICardBrandDetector brandDetector,
        IMemberFundsGate gate,
        IDateTimeProvider dateTime,
        ILogger<RecurringBillingProcessor> logger)
    {
        _db               = db;
        _commissionBalance = commissionBalance;
        _scheduler        = scheduler;
        _router           = router;
        _orchestrator     = orchestrator;
        _brandDetector    = brandDetector;
        _gate             = gate;
        _dateTime         = dateTime;
        _logger           = logger;
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
            var referenceDate  = state.LastSuccessfulBillingDate ?? state.BillingAnchorDate;
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
        //    vez. El saldo se lee DENTRO del candado. El criterio sigue siendo el disponible
        //    (GetAvailableAsync): pasar al gastable cambiaría qué renovaciones se pagan con comisiones y
        //    está pendiente de Nick (Plan K, R7).
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
    /// Paga con comisiones bajo el candado del miembro. Null = el saldo no alcanza y se pasa a la
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
            await CancelOrderAsync(order, now, ct);
            return Result<RecurringBillingProcessorResult>.Failure(MemberFundsBusyCode,
                $"Funds of member {state.MemberId} are busy; the renewal will be retried on the next pass.");
        }

        await using (tx)
        {
            var balance = await _commissionBalance.GetAvailableAsync(state.MemberId, ct);
            if (balance.Available < amountDue)
            {
                await tx.RollbackAsync(CancellationToken.None);
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

                await tx.RollbackAsync(CancellationToken.None);
                await ForgetRolledBackAsync(before);
                await CancelOrderAfterRollbackAsync(order, now);
                return result;
            }
            catch (Exception original)
            {
                await tx.RollbackAsync(CancellationToken.None);
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
    /// Tras deshacer, lo que el contexto creía guardado dentro de la transacción ya no lo está: lo que
    /// se añadió se suelta y lo que ya estaba se relee de la base.
    /// </summary>
    private async Task ForgetRolledBackAsync(HashSet<object> before)
    {
        foreach (var entry in _db.ChangeTracker.Entries().ToList())
        {
            if (!before.Contains(entry.Entity))
                entry.State = EntityState.Detached;
            else
                await entry.ReloadAsync(CancellationToken.None);
        }
    }

    /// <summary>Cancela el pedido (creado y guardado antes del candado) si sigue en Processing.</summary>
    private async Task CancelOrderAfterRollbackAsync(Orders order, DateTime now)
    {
        if (_db.Entry(order).State == EntityState.Detached || order.Status != OrderStatus.Processing) return;

        order.Status         = OrderStatus.Cancelled;
        order.LastUpdateDate = now;
        order.LastUpdateBy   = Actor;
        await _db.SaveChangesAsync(CancellationToken.None);
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
            AttemptIndex                 = 0,
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
            await LiftTerminalMembershipStatusAsync(state.MemberId, now, ct);

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
        {
            // Card charge failed
            await CancelOrderAsync(order, now, ct);

            _scheduler.Advance(state, plan, succeeded: false, attemptDate: now);
            state.LastFailureReason = chargeResult.Error;
            state.LastUpdateDate    = now;
            state.LastUpdateBy      = Actor;

            var failAttempt = new RecurringBillingAttempt
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
                FailureReason              = chargeResult.Error,
                CreatedBy                  = Actor,
                CreationDate               = now
            };
            _db.RecurringBillingAttempts.Add(failAttempt);

            if (state.Status == RecurringBillingStatus.Stopped)
                await ApplyTerminalMembershipStatusAsync(state, plan, ct);

            await _db.SaveChangesAsync(ct);

            return Result<RecurringBillingProcessorResult>.Success(new RecurringBillingProcessorResult
            {
                BillingStateId = state.Id,
                Outcome        = "Failed",
                FundingSource  = "CreditCard",
                FailureReason  = chargeResult.Error
            });
        }

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

        // Plan K (regla de Nick: nunca se cobra con el stub). El gateway de Spreedly todavía devuelve un
        // "éxito" simulado. Eso no es dinero: ni se renueva ni avanza el estado, para que el día que el
        // cobro sea real se intente de verdad. El pedido se cancela. El PaymentHistory que el orquestador
        // ya escribió queda (es suyo): §13 dice cómo encontrarlo.
        if (CardChargeOutcomes.IsSimulated(outcome.GatewayTransactionId))
        {
            _logger.LogCritical(
                "Renovación {StateId} de {MemberId}: la tarjeta devolvió un cobro simulado ({TxId}). No se renueva; " +
                "el cobro con tarjeta del motor no puede darse por bueno mientras el gateway sea un stub.",
                state.Id, state.MemberId, outcome.GatewayTransactionId);
            await CancelOrderAsync(order, now, ct);
            return Result<RecurringBillingProcessorResult>.Failure(TokenPurchaseErrors.CardSimulatedCharge,
                "The card gateway returned a simulated charge; the renewal was not applied.");
        }

        // Immediate success
        order.Status         = OrderStatus.Paid;
        order.LastUpdateDate = now;
        order.LastUpdateBy   = Actor;

        _scheduler.Advance(state, plan, succeeded: true, attemptDate: now);
        state.LastUpdateDate = now;
        state.LastUpdateBy   = Actor;

        UpdateSubscription(subscription, order.Id, now);

        var successAttempt = new RecurringBillingAttempt
        {
            SubscriptionBillingStateId = state.Id,
            MemberId                   = state.MemberId,
            ProductId                  = productId,
            AttemptIndex               = 0,
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
            await LiftTerminalMembershipStatusAsync(state.MemberId, now, ct);

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

    private async Task LiftTerminalMembershipStatusAsync(string memberId, DateTime now, CancellationToken ct)
    {
        var subscription = await _db.MembershipSubscriptions
            .Where(s => s.MemberId == memberId
                     && (s.SubscriptionStatus == MembershipStatus.HoldByBilling
                         || s.SubscriptionStatus == MembershipStatus.Expired)
                     && !s.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (subscription is null) return;

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
