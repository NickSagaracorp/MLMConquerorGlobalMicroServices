using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SharedAPICenter.Features.ProcessPaymentWebhook;

/// <summary>
/// Processes inbound payment webhook events from external payment providers.
///
/// Business rules:
/// - If a PaymentHistory record with the given GatewayTransactionId already exists
///   and is in a terminal state (Captured or Failed), the handler is idempotent
///   and returns success without any modifications.
/// - "payment.success" → TransactionStatus = Captured; if the linked Order exists,
///   its Status is advanced to Completed.
/// - "payment.failed"  → TransactionStatus = Failed.
/// - If no PaymentHistory exists yet, a new record is created with the correct status.
/// </summary>
public class ProcessPaymentWebhookHandler : IRequestHandler<ProcessPaymentWebhookCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ProcessPaymentWebhookHandler(AppDbContext db, IDateTimeProvider clock)
    {
        _db    = db;
        _clock = clock;
    }

    public async Task<Result<bool>> Handle(
        ProcessPaymentWebhookCommand request,
        CancellationToken ct = default)
    {
        var payload = request.Payload;

        var targetStatus = ResolveStatus(payload.Event);
        if (targetStatus is null)
            return Result<bool>.Failure(
                "WEBHOOK_UNKNOWN_EVENT",
                $"Unknown payment event '{payload.Event}'.");

        var existing = await _db.PaymentHistories
            .FirstOrDefaultAsync(
                p => p.GatewayTransactionId == payload.TransactionId,
                ct);

        if (existing is not null)
        {
            // Idempotency guard — terminal states must not be overwritten
            if (IsTerminalStatus(existing.TransactionStatus))
                return Result<bool>.Success(true);

            existing.TransactionStatus = targetStatus.Value;
            existing.ProcessedAt       = _clock.Now;
            existing.LastUpdateDate    = _clock.Now;
            existing.LastUpdateBy      = $"webhook:{request.Provider}";
        }
        else
        {
            // Create a new PaymentHistory record from the webhook payload
            var newRecord = new PaymentHistory
            {
                OrderId              = payload.OrderId ?? string.Empty,
                MemberId             = payload.MemberId ?? string.Empty,
                Amount               = payload.Amount,
                GatewayName          = request.Provider,
                GatewayTransactionId = payload.TransactionId,
                TransactionStatus    = targetStatus.Value,
                ProcessedAt          = _clock.Now,
                CreationDate         = _clock.Now,
                CreatedBy            = $"webhook:{request.Provider}",
                LastUpdateDate       = _clock.Now,
                LastUpdateBy         = $"webhook:{request.Provider}"
            };

            await _db.PaymentHistories.AddAsync(newRecord, ct);
        }

        if (targetStatus == PaymentHistoryTransactionStatus.Captured
            && !string.IsNullOrWhiteSpace(payload.OrderId))
        {
            var order = await _db.Orders
                .FirstOrDefaultAsync(o => o.Id == payload.OrderId, ct);

            if (order is not null
                && order.Status != OrderStatus.Completed
                && order.Status != OrderStatus.Refunded
                && order.Status != OrderStatus.Cancelled)
            {
                order.Status         = OrderStatus.Completed;
                order.LastUpdateDate = _clock.Now;
                order.LastUpdateBy   = $"webhook:{request.Provider}";
            }
        }

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }


    private static PaymentHistoryTransactionStatus? ResolveStatus(string eventName) =>
        eventName.ToLowerInvariant() switch
        {
            "payment.success" => PaymentHistoryTransactionStatus.Captured,
            "payment.failed"  => PaymentHistoryTransactionStatus.Failed,
            _                 => null
        };

    /// <summary>
    /// Terminal states — once reached the record must never be modified by a
    /// webhook retry.
    /// </summary>
    private static bool IsTerminalStatus(PaymentHistoryTransactionStatus status) =>
        status == PaymentHistoryTransactionStatus.Captured
        || status == PaymentHistoryTransactionStatus.Failed
        || status == PaymentHistoryTransactionStatus.Refunded
        || status == PaymentHistoryTransactionStatus.Voided;
}
