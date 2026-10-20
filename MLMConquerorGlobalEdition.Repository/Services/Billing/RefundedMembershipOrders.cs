using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
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
    /// ¿La devolucion es total? Solo una total cancela la membresia; una parcial (un ajuste de importe) solo marca el pago y
    /// el pedido como hasta ahora. Un aviso sin importe (0) cuenta como total: la pasarela avisa del pago entero.
    /// </summary>
    public static bool IsFullRefund(decimal refunded, decimal paid) => refunded <= 0 || refunded >= paid;

    /// <summary>
    /// Cancela la suscripcion activa cuyo pedido vigente es <paramref name="orderId"/>: su LastOrderId, o la que el pedido
    /// senala por MembershipSubscriptionId si aun no tiene LastOrderId. Una renovacion antigua (no es el pedido vigente)
    /// o una suscripcion que ya no esta activa no cambian. Devuelve la cancelada, o null. No guarda.
    /// </summary>
    public static async Task<MembershipSubscription?> CancelSubscriptionAsync(
        AppDbContext db, string orderId, DateTime now, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(orderId)) return null;

        var senalada = await db.Orders.AsNoTracking().Where(o => o.Id == orderId)
            .Select(o => o.MembershipSubscriptionId).FirstOrDefaultAsync(ct);

        var subscription = await db.MembershipSubscriptions
            .Where(s => !s.IsDeleted
                     && s.SubscriptionStatus == MembershipStatus.Active
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
