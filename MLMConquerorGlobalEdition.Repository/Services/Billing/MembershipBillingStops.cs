using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Billing;

/// <summary>
/// Cuándo una membresía ya no se cobra y cómo se para su estado de cobro recurrente (Plan K, revisión
/// C2). Lo usan el motor de Billing y los caminos de cancelación de SignupAPI, para que el motor nunca
/// vuelva a cobrar a quien canceló.
/// </summary>
public static class MembershipBillingStops
{
    public const string ReasonMembershipCancelled = "MEMBERSHIP_CANCELLED";
    public const string ReasonMembershipFree      = "MEMBERSHIP_FREE";

    /// <summary>
    /// Plan Q (tercera revisión del bloque 2, C-a): otra suscripción reemplazó a esta (subida o bajada). Su estado de cobro
    /// se para y no se cobra ni se reactiva nunca más: la que se renueva es la nueva.
    /// </summary>
    public const string ReasonMembershipReplaced  = "MEMBERSHIP_REPLACED";

    /// <summary>La suscripción del estado está borrada o no existe: no hay nada que renovar.</summary>
    public const string ReasonSubscriptionMissing = "SUBSCRIPTION_MISSING";

    /// <summary>
    /// Null si la suscripción se puede cobrar; si no, el motivo. Cancelada, con la cancelación programada
    /// ya cumplida (IsAutoRenew apagado y CancellationDate hoy o antes, en hora de servidor) o gratuita.
    /// IsAutoRenew apagado sin fecha de cancelación NO para: muchas filas lo tienen así por defecto.
    /// </summary>
    public static string? WhyNotBillable(MembershipSubscription subscription, DateTime now)
    {
        if (IsCancelled(subscription.SubscriptionStatus, subscription.IsAutoRenew, subscription.CancellationDate, now))
            return ReasonMembershipCancelled;
        if (subscription.IsFree) return ReasonMembershipFree;
        return null;
    }

    /// <summary>
    /// ¿Está cancelada? Cancelled, o la cancelación programada ya cumplida (IsAutoRenew apagado y
    /// CancellationDate hoy o antes, en hora de servidor). La misma regla para el motor y para la cuota.
    /// </summary>
    public static bool IsCancelled(MembershipStatus status, bool isAutoRenew, DateTime? cancellationDate, DateTime now) =>
        status == MembershipStatus.Cancelled
        || (!isAutoRenew && cancellationDate is { } c && c.Date <= now.Date);

    /// <summary>
    /// Plan Q (tercera revisión del bloque 2, C-a): ¿la reemplazó otra? Caducada y con otra suscripción del mismo miembro,
    /// no borrada, creada a la vez o después y que viene de su nivel (PreviousMembershipLevelId; una renovación no lo
    /// cambia). Una caducada por el motor sin reemplazo no lo está: se cobra y se reactiva como siempre.
    /// </summary>
    public static async Task<bool> IsReplacedAsync(AppDbContext db, MembershipSubscription subscription, CancellationToken ct = default) =>
        subscription.SubscriptionStatus == MembershipStatus.Expired
        && await db.MembershipSubscriptions.AsNoTracking().AnyAsync(o =>
               o.MemberId == subscription.MemberId
               && o.Id != subscription.Id
               && !o.IsDeleted
               && o.PreviousMembershipLevelId == subscription.MembershipLevelId
               && o.CreationDate >= subscription.CreationDate, ct).ConfigureAwait(false);

    /// <summary>
    /// Plan Q (tercera revisión del bloque 2, C-a): una subida o bajada caduca la suscripción vieja; su estado de cobro se
    /// para en el mismo guardado (no guarda). La nueva tendrá el suyo (el barrido lo crea si falta): uno vivo por miembro.
    /// </summary>
    public static Task StopForReplacedAsync(AppDbContext db, string replacedSubscriptionId, DateTime now, string actor,
        CancellationToken ct = default) =>
        StopForSubscriptionsAsync(db, new[] { replacedSubscriptionId }, ReasonMembershipReplaced, now, actor, ct);

    /// <summary>Para el estado (lo deja rastreado; guarda quien llama).</summary>
    public static void Stop(SubscriptionBillingState state, string reason, DateTime now, string actor)
    {
        state.Status            = RecurringBillingStatus.Stopped;
        state.LastFailureReason = reason;
        state.LastUpdateDate    = now;
        state.LastUpdateBy      = actor;
    }

    /// <summary>
    /// Para los estados de cobro de esas suscripciones que sigan vivos. No guarda: va en el mismo
    /// SaveChanges que la cancelación.
    /// </summary>
    public static async Task StopForSubscriptionsAsync(
        AppDbContext db, IReadOnlyCollection<string> subscriptionIds, string reason, DateTime now, string actor,
        CancellationToken ct = default)
    {
        if (subscriptionIds.Count == 0) return;
        var states = await db.SubscriptionBillingStates
            .Where(s => s.MembershipSubscriptionId != null
                     && subscriptionIds.Contains(s.MembershipSubscriptionId)
                     && s.Status != RecurringBillingStatus.Stopped)
            .ToListAsync(ct);
        foreach (var s in states) Stop(s, reason, now, actor);
    }
}
