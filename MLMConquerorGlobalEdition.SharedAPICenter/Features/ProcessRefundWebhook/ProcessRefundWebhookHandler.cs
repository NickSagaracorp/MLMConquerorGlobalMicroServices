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
/// - Loads PaymentHistory by GatewayTransactionId. Not found: failure, so the provider receives a non-2xx and retries.
/// - Already Refunded (a repeated notice): success without changes.
/// - Sets TransactionStatus = Refunded on the PaymentHistory record.
/// - Revision de Q3 (C1, M2): the endpoint is public and UNSIGNED (no provider HMAC yet: a pre-production blocker, see the
///   Plan Q document). Anyone can post a payment.success that creates a Captured PaymentHistory for any order, and then a
///   refund for it. So the notice touches an order only through <see cref="RefundedMembershipOrders.IsOwnCapturedPaymentOf"/>:
///   the payment was created by our own charge flow (not by the webhook), was Captured before this notice, points to that
///   order (the payload OrderId is ignored) and the order (not deleted) belongs to the payment's member. Only then the
///   order becomes Refunded.
/// - Plan Q (Q3): on top of that, a FULL refund or chargeback (explicit amount &gt; 0 and &gt;= the payment; 0 or missing is
///   not full) of the current order of a membership cancels it (RefundedMembershipOrders, shared with the admin refund)
///   and stops its recurring billing.
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

        // Revision de Q3 (M8): la pasarela repite avisos; el repetido no cambia nada.
        if (payment.TransactionStatus == PaymentHistoryTransactionStatus.Refunded)
            return Result<bool>.Success(true);

        var now    = _clock.Now;
        var actor  = RefundedMembershipOrders.WebhookActor(request.Provider);
        var before = payment.TransactionStatus;

        payment.TransactionStatus = PaymentHistoryTransactionStatus.Refunded;
        payment.ProcessedAt       = now;
        payment.LastUpdateDate    = now;
        payment.LastUpdateBy      = actor;

        // Revision de Q3 (C1, M2): solo el pedido del propio pago, si el pago es nuestro y estaba cobrado. Sin firma de la
        // pasarela (pendiente antes de produccion), un aviso no puede decidir sobre un pedido ajeno.
        Orders? order = null;
        if (!string.IsNullOrWhiteSpace(payment.OrderId))
        {
            order = await _db.Orders
                .FirstOrDefaultAsync(o => o.Id == payment.OrderId && !o.IsDeleted, ct);
            if (order is not null && !RefundedMembershipOrders.IsOwnCapturedPaymentOf(payment, before, order))
                order = null;

            if (order is not null)
            {
                order.Status         = OrderStatus.Refunded;
                order.LastUpdateDate = now;
                order.LastUpdateBy   = actor;
            }
        }

        // Plan Q (Q3, decidido por Nick): una devolucion o un contracargo TOTAL del pedido vigente de la membresia la
        // cancela (el interceptor resta sus puntos del miembro y de su linea), con el mismo codigo que la devolucion desde
        // el admin. Un aviso parcial, sin importe o sobre un pedido que no pasa la guarda de arriba no la toca. Bajo el
        // candado de puntos del miembro, como en el admin.
        var cancela = order is not null && RefundedMembershipOrders.IsFullRefund(payload.Amount, payment.Amount);
        if (!cancela)
        {
            await _db.SaveChangesAsync(ct);
            return Result<bool>.Success(true);
        }

        await MemberPointsLocks.AcquireAsync(_db, new[] { order!.MemberId }, ct);
        try
        {
            await RefundedMembershipOrders.CancelSubscriptionAsync(_db, order.Id, now, actor, ct);
            await _db.SaveChangesAsync(ct);
        }
        finally
        {
            await MemberPointsLocks.ReleaseAsync(_db);
        }
        return Result<bool>.Success(true);
    }
}
