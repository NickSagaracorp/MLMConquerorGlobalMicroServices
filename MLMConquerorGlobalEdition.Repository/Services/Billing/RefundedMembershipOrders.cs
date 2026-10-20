using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Billing;

/// <summary>
/// Plan Q (Q3, decidido por Nick: «las devoluciones y contracargos deben regresar los puntos»): la devolucion o el
/// contracargo del pedido VIGENTE de una membresia activa la cancela. El interceptor (MembershipPointsDeltas) resta lo que
/// aportaba del miembro y de toda su linea; su estado de cobro se para para que el motor no la vuelva a cobrar. Lo usan la
/// devolucion desde el admin (Billing) y el aviso de la pasarela (SharedAPICenter), el mismo codigo para los dos.
/// <para>
/// Quien llama toma antes el candado de puntos del miembro (<see cref="Teams.MemberPointsLocks"/>) si va a leer y decidir,
/// y guarda: aqui no se guarda.
/// </para>
/// </summary>
public static class RefundedMembershipOrders
{
    /// <summary>
    /// Autor (CreatedBy/LastUpdateBy) de lo que escribe el webhook publico de pagos: "webhook:{proveedor}". Revision de Q3
    /// (C1): un PaymentHistory creado con este prefijo lo creo un aviso sin firma, no nuestro flujo de cobro.
    /// </summary>
    public const string WebhookActorPrefix = "webhook:";

    public static string WebhookActor(string provider) => WebhookActorPrefix + provider;

    /// <summary>
    /// ¿Lo creo el webhook publico de pagos? Nuestros flujos de cobro (Charge, RenewMembership, el motor, la cuota, tokens,
    /// Entrax) crean el pago Captured con su propio autor; solo ProcessPaymentWebhookHandler lo crea con
    /// <see cref="WebhookActorPrefix"/>.
    /// </summary>
    public static bool CreatedByWebhook(PaymentHistory payment) =>
        (payment.CreatedBy ?? string.Empty).StartsWith(WebhookActorPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Revision de Q3 (C1, M2): ¿puede un aviso de la pasarela devolver este pedido? Solo si el pago lo creo nuestro flujo
    /// de cobro (no el webhook), estaba cobrado (Captured) antes del aviso, senala ese pedido (OrderId no vacio e igual) y
    /// el pedido es del mismo miembro que el pago. El OrderId del aviso no cuenta: el endpoint es publico y sin firma.
    /// </summary>
    public static bool IsOwnCapturedPaymentOf(PaymentHistory payment, PaymentHistoryTransactionStatus statusBeforeNotice,
        Orders order) =>
        !CreatedByWebhook(payment)
        && statusBeforeNotice == PaymentHistoryTransactionStatus.Captured
        && !string.IsNullOrWhiteSpace(payment.OrderId)
        && payment.OrderId == order.Id
        && !string.IsNullOrWhiteSpace(payment.MemberId)
        && order.MemberId == payment.MemberId;

    /// <summary>
    /// ¿La devolucion es total? Solo una total cancela la membresia; una parcial (un ajuste de importe) solo marca el pago y
    /// el pedido como hasta ahora. Revision de Q3 (C1): un aviso sin importe (0) o negativo NO cuenta como total; el importe
    /// tiene que llegar explicito y cubrir el pago.
    /// </summary>
    public static bool IsFullRefund(decimal refunded, decimal paid) => refunded > 0 && refunded >= paid;

    /// <summary>
    /// Cancela la suscripcion viva cuyo pedido vigente es <paramref name="orderId"/>: su LastOrderId, o la que el pedido
    /// senala por MembershipSubscriptionId si aun no tiene LastOrderId. Viva es Active o retenida (revision de Q3, I1:
    /// OnHold, la del admin o de otro origen, y HoldByBilling), para que reactivar la cuenta o un cobro forzado no reviva
    /// una membresia devuelta. Una renovacion antigua (no es el pedido vigente) o una cancelada o caducada no cambian.
    /// Devuelve la cancelada, o null. No guarda.
    /// </summary>
    public static async Task<MembershipSubscription?> CancelSubscriptionAsync(
        AppDbContext db, string orderId, DateTime now, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(orderId)) return null;

        var senalada = await db.Orders.AsNoTracking().Where(o => o.Id == orderId)
            .Select(o => o.MembershipSubscriptionId).FirstOrDefaultAsync(ct);

        var subscription = await db.MembershipSubscriptions
            .Where(s => !s.IsDeleted
                     && (s.SubscriptionStatus == MembershipStatus.Active
                         || s.SubscriptionStatus == MembershipStatus.OnHold
                         || s.SubscriptionStatus == MembershipStatus.HoldByBilling)
                     && (s.LastOrderId == orderId || (s.LastOrderId == null && s.Id == senalada)))
            .OrderByDescending(s => s.CreationDate)
            .FirstOrDefaultAsync(ct);
        if (subscription is null) return null;

        subscription.SubscriptionStatus  = MembershipStatus.Cancelled;
        subscription.CancellationDate    = now;
        subscription.EndDate             = now;
        subscription.IsAutoRenew         = false;
        subscription.HeldByAccountStatus = null;
        subscription.LastUpdateDate      = now;
        subscription.LastUpdateBy        = actor;

        await MembershipBillingStops.StopForSubscriptionsAsync(db, new[] { subscription.Id },
            MembershipBillingStops.ReasonMembershipRefunded, now, actor, ct);
        return subscription;
    }
}
