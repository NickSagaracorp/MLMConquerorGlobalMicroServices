using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Features.RenewMembership;

public class RenewMembershipHandler : IRequestHandler<RenewMembershipCommand, Result<ChargeResponse>>
{
    // USA and Canada: use the new routing engine with recurring fallback chain.
    private static readonly HashSet<string> RecurringRoutedCountries =
        new(StringComparer.OrdinalIgnoreCase) { "US", "CA" };

    private readonly AppDbContext               _db;
    private readonly IGatewayResolver           _legacyGatewayResolver;
    private readonly IGatewayRouter             _router;
    private readonly IGatewayChargeOrchestrator _orchestrator;
    private readonly ICardBrandDetector         _brandDetector;
    private readonly ICurrentUserService        _currentUser;
    private readonly IDateTimeProvider          _dateTime;

    public RenewMembershipHandler(
        AppDbContext db,
        IGatewayResolver legacyGatewayResolver,
        IGatewayRouter router,
        IGatewayChargeOrchestrator orchestrator,
        ICardBrandDetector brandDetector,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db                    = db;
        _legacyGatewayResolver = legacyGatewayResolver;
        _router                = router;
        _orchestrator          = orchestrator;
        _brandDetector         = brandDetector;
        _currentUser           = currentUser;
        _dateTime              = dateTime;
    }

    public async Task<Result<ChargeResponse>> Handle(RenewMembershipCommand command, CancellationToken ct)
    {
        var req = command.Request;
        var now = _dateTime.Now;

        // Load subscription with membership level
        var subscription = string.IsNullOrWhiteSpace(req.SubscriptionId)
            ? await _db.MembershipSubscriptions
                .Include(s => s.MembershipLevel)
                .FirstOrDefaultAsync(s => s.MemberId == req.MemberId
                                          && s.SubscriptionStatus == Domain.Entities.Membership.MembershipStatus.Active
                                          && !s.IsDeleted, ct)
            : await _db.MembershipSubscriptions
                .Include(s => s.MembershipLevel)
                .FirstOrDefaultAsync(s => s.Id == req.SubscriptionId
                                          && s.MemberId == req.MemberId
                                          && !s.IsDeleted, ct);

        if (subscription is null)
            return Result<ChargeResponse>.Failure("SUBSCRIPTION_NOT_FOUND",
                $"No active subscription found for member '{req.MemberId}'.");

        if (subscription.MembershipLevel is null)
            return Result<ChargeResponse>.Failure("MEMBERSHIP_LEVEL_NOT_FOUND",
                "Membership level data is missing for the subscription.");

        if (subscription.IsFree)
            return Result<ChargeResponse>.Failure("RENEWAL_NOT_REQUIRED",
                "Free membership subscriptions do not require renewal.");

        var renewalPrice = subscription.MembershipLevel.RenewalPrice;
        if (renewalPrice <= 0)
            return Result<ChargeResponse>.Failure("INVALID_RENEWAL_PRICE",
                $"Invalid renewal price ({renewalPrice}) for membership level '{subscription.MembershipLevel.Name}'.");

        // Determine cardholder country from member profile
        var memberProfile = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == req.MemberId, ct);

        var cardholderCountry = memberProfile?.Country?.Length == 2
            ? memberProfile.Country.ToUpperInvariant()
            : "US"; // default if unknown

        // ── Routing path decision ──────────────────────────────────────────
        if (RecurringRoutedCountries.Contains(cardholderCountry))
        {
            return await HandleWithRoutingEngineAsync(
                req, subscription, renewalPrice, cardholderCountry, now, ct);
        }
        else
        {
            // Non-USA/Canada: use the legacy path (wallet gateway)
            return await HandleLegacyAsync(req, subscription, renewalPrice, now, ct);
        }
    }

    // ── New routing engine path (USA + Canada) ────────────────────────────

    private async Task<Result<ChargeResponse>> HandleWithRoutingEngineAsync(
        MembershipRenewalRequest req,
        Domain.Entities.Membership.MembershipSubscription subscription,
        decimal renewalPrice,
        string cardholderCountry,
        DateTime now,
        CancellationToken ct)
    {
        // Build order
        string orderNo;
        do { orderNo = OrderNumberHelper.Generate(subscription.MembershipLevel!.Name, now); }
        while (await _db.Orders.AnyAsync(o => o.OrderNo == orderNo, ct));

        var order = new Orders
        {
            MemberId                = req.MemberId,
            MembershipSubscriptionId = subscription.Id,
            OrderNo                 = orderNo,
            TotalAmount             = renewalPrice,
            Status                  = OrderStatus.Processing,
            OrderDate               = now,
            Notes                   = $"Membership renewal — {subscription.MembershipLevel!.Name}",
            CreationDate            = now,
            CreatedBy               = _currentUser.UserId,
            LastUpdateDate          = now,
            LastUpdateBy            = _currentUser.UserId
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        // Resolve card brand from member's credit card (Visa/MC are the common recurring cards)
        var creditCard = await _db.CreditCards
            .AsNoTracking()
            .Where(c => c.MemberId == req.MemberId && c.IsDefault && !c.IsDeleted)
            .OrderBy(c => c.Priority)
            .FirstOrDefaultAsync(ct);

        var cardBrand = creditCard is not null
            ? _brandDetector.Detect(creditCard.First6)
            : CardBrand.Visa; // safe default for routing

        var routingCtx = new GatewayRoutingContext
        {
            OperationType        = BillingOperationType.Payment,
            CardBrand            = cardBrand,
            CardholderCountryIso = cardholderCountry,
            AmountUsd            = renewalPrice,
            MemberId             = req.MemberId
        };

        var planResult = await _router.ResolveAsync(routingCtx, ct);
        if (!planResult.IsSuccess)
            return Result<ChargeResponse>.Failure(planResult.ErrorCode!, planResult.Error!);

        var chargeReq = new OrchestratorChargeRequest
        {
            MemberId             = req.MemberId,
            TokenizedCardRef     = creditCard?.CardToken,
            NetworkTransactionId = creditCard?.GatewayToken,
            Description          = $"Membership renewal: {subscription.MembershipLevel!.Name}",
            OrderId              = order.Id,
            IsRecurring          = true
        };

        var result = await _orchestrator.ExecuteAsync(planResult.Value!, routingCtx, chargeReq, ct);
        if (!result.IsSuccess)
        {
            order.Status         = OrderStatus.Cancelled;
            order.LastUpdateDate = now;
            order.LastUpdateBy   = _currentUser.UserId;
            await _db.SaveChangesAsync(ct);
            return Result<ChargeResponse>.Failure(result.ErrorCode!, result.Error!);
        }

        var outcome = result.Value!;

        if (outcome.Status != "Scheduled")
        {
            // Update order and subscription on immediate success
            order.Status         = OrderStatus.Paid;
            order.LastUpdateDate = now;
            order.LastUpdateBy   = _currentUser.UserId;

            subscription.StartDate    = now;
            subscription.ChangeReason = SubscriptionChangeReason.Renewal;
            subscription.LastOrderId  = order.Id;
            subscription.LastUpdateDate = now;
            subscription.LastUpdateBy   = _currentUser.UserId;

            await _db.SaveChangesAsync(ct);
        }

        return Result<ChargeResponse>.Success(new ChargeResponse
        {
            PaymentHistoryId     = outcome.PaymentHistoryId,
            GatewayTransactionId = outcome.GatewayTransactionId,
            Amount               = outcome.AmountCharged,
            Gateway              = outcome.ProcessorUsed,
            Status               = outcome.Status
        });
    }

    // ── Legacy wallet path (non-USA/Canada) ───────────────────────────────

    private async Task<Result<ChargeResponse>> HandleLegacyAsync(
        MembershipRenewalRequest req,
        Domain.Entities.Membership.MembershipSubscription subscription,
        decimal renewalPrice,
        DateTime now,
        CancellationToken ct)
    {
        var preferredWallet = await _db.Wallets
            .FirstOrDefaultAsync(w => w.MemberId == req.MemberId
                                       && w.IsPreferred
                                       && !w.IsDeleted, ct);

        if (preferredWallet is null)
            return Result<ChargeResponse>.Failure("NO_PREFERRED_WALLET",
                $"No preferred wallet found for member '{req.MemberId}'. Cannot process renewal.");

        IGatewayService gateway;
        try
        {
            gateway = _legacyGatewayResolver.Resolve(preferredWallet.WalletType);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ChargeResponse>.Failure("GATEWAY_NOT_SUPPORTED", ex.Message);
        }

        string orderNo;
        do { orderNo = OrderNumberHelper.Generate(subscription.MembershipLevel!.Name, now); }
        while (await _db.Orders.AnyAsync(o => o.OrderNo == orderNo, ct));

        var order = new Orders
        {
            MemberId                = req.MemberId,
            MembershipSubscriptionId = subscription.Id,
            OrderNo                 = orderNo,
            TotalAmount             = renewalPrice,
            Status                  = OrderStatus.Processing,
            OrderDate               = now,
            Notes                   = $"Membership renewal — {subscription.MembershipLevel!.Name}",
            CreationDate            = now,
            CreatedBy               = _currentUser.UserId,
            LastUpdateDate          = now,
            LastUpdateBy            = _currentUser.UserId
        };
        _db.Orders.Add(order);

        var chargeResult = await gateway.ChargeAsync(
            req.MemberId, renewalPrice, "USD",
            $"Membership renewal: {subscription.MembershipLevel!.Name}", ct);

        if (!chargeResult.IsSuccess)
            return Result<ChargeResponse>.Failure(chargeResult.ErrorCode!, chargeResult.Error!);

        var payment = new PaymentHistory
        {
            OrderId              = order.Id,
            MemberId             = req.MemberId,
            Amount               = renewalPrice,
            GatewayName          = preferredWallet.WalletType.ToString(),
            GatewayTransactionId = chargeResult.Value!,
            TransactionStatus    = PaymentHistoryTransactionStatus.Captured,
            ProcessedAt          = now,
            CreationDate         = now,
            CreatedBy            = _currentUser.UserId,
            LastUpdateDate       = now,
            LastUpdateBy         = _currentUser.UserId
        };
        _db.PaymentHistories.Add(payment);

        order.Status         = OrderStatus.Paid;
        order.LastUpdateDate = now;
        order.LastUpdateBy   = _currentUser.UserId;

        subscription.StartDate    = now;
        subscription.ChangeReason = SubscriptionChangeReason.Renewal;
        subscription.LastOrderId  = order.Id;
        subscription.LastUpdateDate = now;
        subscription.LastUpdateBy   = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        return Result<ChargeResponse>.Success(new ChargeResponse
        {
            PaymentHistoryId     = payment.Id,
            GatewayTransactionId = chargeResult.Value!,
            Amount               = renewalPrice,
            Gateway              = preferredWallet.WalletType.ToString(),
            Status               = payment.TransactionStatus.ToString()
        });
    }
}
