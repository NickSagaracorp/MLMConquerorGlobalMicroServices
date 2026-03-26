using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Features.RenewMembership;

public class RenewMembershipHandler : IRequestHandler<RenewMembershipCommand, Result<ChargeResponse>>
{
    private readonly AppDbContext _db;
    private readonly IGatewayResolver _gatewayResolver;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public RenewMembershipHandler(
        AppDbContext db,
        IGatewayResolver gatewayResolver,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _gatewayResolver = gatewayResolver;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<ChargeResponse>> Handle(RenewMembershipCommand command, CancellationToken ct)
    {
        var req = command.Request;
        var Now = _dateTime.Now;

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

        // Find member's preferred wallet to determine gateway
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
            gateway = _gatewayResolver.Resolve(preferredWallet.WalletType);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ChargeResponse>.Failure("GATEWAY_NOT_SUPPORTED", ex.Message);
        }

        // Create renewal order
        var order = new Orders
        {
            MemberId = req.MemberId,
            MembershipSubscriptionId = subscription.Id,
            TotalAmount = renewalPrice,
            Status = OrderStatus.Processing,
            OrderDate = Now,
            Notes = $"Membership renewal — {subscription.MembershipLevel.Name}",
            CreationDate = Now,
            CreatedBy = _currentUser.UserId,
            LastUpdateDate = Now,
            LastUpdateBy = _currentUser.UserId
        };
        _db.Orders.Add(order);

        // Charge via gateway
        var chargeResult = await gateway.ChargeAsync(
            req.MemberId,
            renewalPrice,
            "USD",
            $"Membership renewal: {subscription.MembershipLevel.Name}",
            ct);

        if (!chargeResult.IsSuccess)
            return Result<ChargeResponse>.Failure(chargeResult.ErrorCode!, chargeResult.Error!);

        var gatewayTransactionId = chargeResult.Value!;

        // Create payment history
        var payment = new PaymentHistory
        {
            OrderId = order.Id,
            MemberId = req.MemberId,
            Amount = renewalPrice,
            GatewayName = preferredWallet.WalletType.ToString(),
            GatewayTransactionId = gatewayTransactionId,
            TransactionStatus = PaymentHistoryTransactionStatus.Captured,
            ProcessedAt = Now,
            CreationDate = Now,
            CreatedBy = _currentUser.UserId,
            LastUpdateDate = Now,
            LastUpdateBy = _currentUser.UserId
        };
        _db.PaymentHistories.Add(payment);

        // Update order to paid
        order.Status = OrderStatus.Paid;
        order.LastUpdateDate = Now;
        order.LastUpdateBy = _currentUser.UserId;

        // Update subscription
        subscription.StartDate = Now;
        subscription.ChangeReason = SubscriptionChangeReason.Renewal;
        subscription.LastOrderId = order.Id;
        subscription.LastUpdateDate = Now;
        subscription.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        return Result<ChargeResponse>.Success(new ChargeResponse
        {
            PaymentHistoryId = payment.Id,
            GatewayTransactionId = gatewayTransactionId,
            Amount = renewalPrice,
            Gateway = preferredWallet.WalletType.ToString(),
            Status = payment.TransactionStatus.ToString()
        });
    }
}
