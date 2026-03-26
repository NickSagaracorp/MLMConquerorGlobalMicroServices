using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Features.Refund;

public class RefundHandler : IRequestHandler<RefundCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly IGatewayResolver _gatewayResolver;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public RefundHandler(
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

    public async Task<Result<bool>> Handle(RefundCommand command, CancellationToken ct)
    {
        var req = command.Request;

        // Load payment history
        var payment = await _db.PaymentHistories
            .FirstOrDefaultAsync(p => p.Id == req.PaymentHistoryId && !p.IsDeleted, ct);

        if (payment is null)
            return Result<bool>.Failure("PAYMENT_NOT_FOUND",
                $"Payment history '{req.PaymentHistoryId}' not found.");

        // Only Captured (successful) payments can be refunded
        if (payment.TransactionStatus != PaymentHistoryTransactionStatus.Captured)
            return Result<bool>.Failure("PAYMENT_NOT_REFUNDABLE",
                $"Payment cannot be refunded. Current status: {payment.TransactionStatus}.");

        // Parse gateway type from GatewayName
        if (!Enum.TryParse<WalletType>(payment.GatewayName, ignoreCase: true, out var walletType))
            return Result<bool>.Failure("GATEWAY_UNKNOWN",
                $"Cannot determine gateway for payment '{req.PaymentHistoryId}'. GatewayName: '{payment.GatewayName}'.");

        // Only Stripe and eWallet supported for refunds
        if (walletType != WalletType.Dwolla && walletType != WalletType.eWallet)
            return Result<bool>.Failure("REFUND_NOT_SUPPORTED",
                $"Refunds are not supported for gateway '{walletType}' in this version.");

        IGatewayService gateway;
        try
        {
            gateway = _gatewayResolver.Resolve(walletType);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.Failure("GATEWAY_NOT_SUPPORTED", ex.Message);
        }

        var refundAmount = req.Amount ?? payment.Amount;

        if (refundAmount <= 0 || refundAmount > payment.Amount)
            return Result<bool>.Failure("INVALID_REFUND_AMOUNT",
                $"Refund amount must be between 0.01 and {payment.Amount:F2}.");

        var transactionId = payment.GatewayTransactionId ?? string.Empty;
        var refundResult = await gateway.RefundAsync(transactionId, refundAmount, ct);

        if (!refundResult.IsSuccess)
            return Result<bool>.Failure(refundResult.ErrorCode!, refundResult.Error!);

        // Update payment history status
        var Now = _dateTime.Now;
        payment.TransactionStatus = PaymentHistoryTransactionStatus.Refunded;
        payment.FailureReason = req.Reason;
        payment.LastUpdateDate = Now;
        payment.LastUpdateBy = _currentUser.UserId;

        // Update linked order if present
        if (!string.IsNullOrWhiteSpace(payment.OrderId))
        {
            var order = await _db.Orders
                .FirstOrDefaultAsync(o => o.Id == payment.OrderId && !o.IsDeleted, ct);

            if (order is not null)
            {
                order.Status = OrderStatus.Refunded;
                order.LastUpdateDate = Now;
                order.LastUpdateBy = _currentUser.UserId;
            }
        }

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
