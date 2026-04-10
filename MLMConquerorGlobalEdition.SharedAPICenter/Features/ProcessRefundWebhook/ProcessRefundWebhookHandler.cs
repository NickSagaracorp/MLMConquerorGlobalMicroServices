using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SharedAPICenter.Features.ProcessRefundWebhook;

/// <summary>
/// Processes inbound refund webhook events from external payment providers.
///
/// Business rules:
/// - Loads PaymentHistory by GatewayTransactionId.
/// - Sets TransactionStatus = Refunded on the PaymentHistory record.
/// - If a linked Order is found, its Status is set to Refunded.
/// - If no matching PaymentHistory is found, returns a failure result so
///   the provider receives a non-2xx and can retry later.
/// </summary>
public class ProcessRefundWebhookHandler : IRequestHandler<ProcessRefundWebhookCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ProcessRefundWebhookHandler(AppDbContext db, IDateTimeProvider clock)
    {
        _db    = db;
        _clock = clock;
    }

    public async Task<Result<bool>> Handle(
        ProcessRefundWebhookCommand request,
        CancellationToken ct = default)
    {
        var payload = request.Payload;

        var payment = await _db.PaymentHistories
            .FirstOrDefaultAsync(
                p => p.GatewayTransactionId == payload.TransactionId,
                ct);

        if (payment is null)
            return Result<bool>.Failure(
                "PAYMENT_NOT_FOUND",
                $"No payment record found for transaction '{payload.TransactionId}'.");

        payment.TransactionStatus = PaymentHistoryTransactionStatus.Refunded;
        payment.ProcessedAt       = _clock.Now;
        payment.LastUpdateDate    = _clock.Now;
        payment.LastUpdateBy      = $"webhook:{request.Provider}";

        var orderId = !string.IsNullOrWhiteSpace(payload.OrderId)
            ? payload.OrderId
            : payment.OrderId;

        if (!string.IsNullOrWhiteSpace(orderId))
        {
            var order = await _db.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);

            if (order is not null)
            {
                order.Status         = OrderStatus.Refunded;
                order.LastUpdateDate = _clock.Now;
                order.LastUpdateBy   = $"webhook:{request.Provider}";
            }
        }

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
