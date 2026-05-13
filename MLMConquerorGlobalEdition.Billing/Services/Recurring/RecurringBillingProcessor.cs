using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

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
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<RecurringBillingProcessor> _logger;

    private const string Actor = "recurring-billing-engine";

    public RecurringBillingProcessor(
        AppDbContext db,
        ICommissionBalanceService commissionBalance,
        IRecurringBillingScheduler scheduler,
        IGatewayRouter router,
        IGatewayChargeOrchestrator orchestrator,
        ICardBrandDetector brandDetector,
        IDateTimeProvider dateTime,
        ILogger<RecurringBillingProcessor> logger)
    {
        _db               = db;
        _commissionBalance = commissionBalance;
        _scheduler        = scheduler;
        _router           = router;
        _orchestrator     = orchestrator;
        _brandDetector    = brandDetector;
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
                await _db.SaveChangesAsync(ct);

                await ApplyTerminalMembershipStatusAsync(state, plan, ct);
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

        // 6. Commission-balance first?
        if (plan.PayFromCommissionBalanceFirst)
        {
            var balance = await _commissionBalance.GetAvailableAsync(state.MemberId, ct);
            if (balance.Available >= amountDue)
            {
                return await HandleCommissionFundedAsync(
                    state, plan, planProduct, subscription, order, productId, amountDue, now, ct);
            }
        }

        // 7. Fall through to card charge
        return await HandleCardChargeAsync(
            state, plan, subscription, order, productId, amountDue, now, ct);
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
