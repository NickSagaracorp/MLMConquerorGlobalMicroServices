using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Billing;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
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
/// - Plan Q (Q3): a full refund or chargeback of the current order of an Active membership cancels it
///   (RefundedMembershipOrders, shared with the admin refund) and stops its recurring billing.
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

        Orders? order = null;
        if (!string.IsNullOrWhiteSpace(orderId))
        {
            order = await _db.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);

            if (order is not null)
            {
                order.Status         = OrderStatus.Refunded;
                order.LastUpdateDate = _clock.Now;
                order.LastUpdateBy   = $"webhook:{request.Provider}";
            }
        }

        // Plan Q (Q3, decidido por Nick): una devolucion o un contracargo TOTAL del pedido vigente de una membresia activa
        // la cancela (el interceptor resta sus puntos del miembro y de su linea), con el mismo codigo que la devolucion
        // desde el admin. Solo el pedido del propio pago: un OrderId del aviso que no es el del pago no cancela nada (el
        // endpoint es publico). Un aviso parcial tampoco. Bajo el candado de puntos del miembro, como en el admin.
        var cancela = order is not null
            && (string.IsNullOrWhiteSpace(payment.OrderId) || payment.OrderId == order.Id)
            && RefundedMembershipOrders.IsFullRefund(payload.Amount, payment.Amount);
        if (!cancela)
        {
            await _db.SaveChangesAsync(ct);
            return Result<bool>.Success(true);
        }

        await MemberPointsLocks.AcquireAsync(_db, new[] { order!.MemberId }, ct);
        try
        {
            await RefundedMembershipOrders.CancelSubscriptionAsync(
                _db, order.Id, _clock.Now, $"webhook:{request.Provider}", ct);
            await _db.SaveChangesAsync(ct);
        }
        finally
        {
            await MemberPointsLocks.ReleaseAsync(_db);
        }
        return Result<bool>.Success(true);
    }
}
