using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
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

    /// <summary>Plan Q (Q3): la cancelo una devolucion o un contracargo de su pedido vigente.</summary>
    public const string ReasonMembershipRefunded  = "MEMBERSHIP_REFUNDED";

    /// <summary>
    /// Plan Q (Q3): retenida (OnHold), p. ej. por suspender o inactivar la cuenta desde el admin. No se cobra; al restaurarla
    /// el admin, su estado de cobro parado con este motivo se reanuda (<see cref="ResumeHeldAsync"/>).
    /// </summary>
    public const string ReasonMembershipOnHold    = "MEMBERSHIP_ON_HOLD";

    /// <summary>
    /// Revision de Q3 (I3): la cuenta del miembro esta terminada. No se cobra nunca mas (como la cuota anual): el estado
    /// se para, tambien forzado.
    /// </summary>
    public const string ReasonAccountTerminated   = "ACCOUNT_TERMINATED";

    /// <summary>
    /// Revision de Q3 (I3): la cuenta del miembro esta suspendida. No se cobra mientras lo este (como la cuota anual, se
    /// pospone): el estado no se para ni se revive forzado.
    /// </summary>
    public const string ReasonAccountSuspended    = "ACCOUNT_SUSPENDED";

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
        // Plan Q (Q3): una retenida no se cobra (antes se cobraba y seguia OnHold). Al restaurarla el admin, ResumeHeldAsync
        // reanuda el estado parado con este motivo.
        if (subscription.SubscriptionStatus == MembershipStatus.OnHold) return ReasonMembershipOnHold;
        return null;
    }

    /// <summary>
    /// Revision de Q3 (I3): null si la cuenta del miembro deja cobrar su membresia; si no, el motivo. Terminated
    /// (<see cref="ReasonAccountTerminated"/>) y Suspended (<see cref="ReasonAccountSuspended"/>) no se cobran, igual que la
    /// cuota anual. Inactive si: es la cuenta de quien cancelo su membresia, y la que retuvo el admin ya la para
    /// <see cref="WhyNotBillable"/> (OnHold). Sin perfil no se decide aqui.
    /// </summary>
    public static string? WhyAccountNotBillable(MemberAccountStatus? accountStatus) => accountStatus switch
    {
        MemberAccountStatus.Terminated => ReasonAccountTerminated,
        MemberAccountStatus.Suspended  => ReasonAccountSuspended,
        _                              => null
    };

    /// <summary>
    /// Plan Q (Q3): desde cuando cuentan los dias sin cobrar del tope del plan (StopAfterUnbilledDays): lo mas reciente
    /// entre el ultimo cobro y el ancla del ciclo. Normalmente el ultimo cobro; tras reanudar una retencion larga, el ancla
    /// que ResumeHeldAsync puso en la reanudacion (si no, el tope pararia la reanudada sin intentar cobrarla).
    /// </summary>
    public static DateTime UnbilledSince(SubscriptionBillingState state) =>
        state.LastSuccessfulBillingDate is { } ultimo && ultimo > state.BillingAnchorDate ? ultimo : state.BillingAnchorDate;

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
    /// <para>
    /// Revision de Q3 (I1): uno ya parado por retencion (<see cref="ReasonMembershipOnHold"/>) se vuelve a sellar con el
    /// nuevo motivo (devuelta, cancelada...): asi <see cref="ResumeHeldAsync"/> ya no lo reanuda.
    /// </para>
    /// </summary>
    public static async Task StopForSubscriptionsAsync(
        AppDbContext db, IReadOnlyCollection<string> subscriptionIds, string reason, DateTime now, string actor,
        CancellationToken ct = default)
    {
        if (subscriptionIds.Count == 0) return;
        var restamp = reason != ReasonMembershipOnHold;
        var states = await db.SubscriptionBillingStates
            .Where(s => s.MembershipSubscriptionId != null
                     && subscriptionIds.Contains(s.MembershipSubscriptionId)
                     && (s.Status != RecurringBillingStatus.Stopped
                         || (restamp && s.LastFailureReason == ReasonMembershipOnHold)))
            .ToListAsync(ct);
        foreach (var s in states) Stop(s, reason, now, actor);
    }

    /// <summary>
    /// Plan Q (Q3): reanuda los estados de cobro de esas suscripciones que se pararon por retencion
    /// (<see cref="ReasonMembershipOnHold"/>); uno parado por otro motivo se queda parado. Si el ciclo vencio durante la
    /// retencion, el ciclo se reancla a hoy (fecha de cobro y ancla): se cobra hoy y el tope de dias sin cobrar cuenta desde
    /// la reanudacion, no desde antes de la retencion. No guarda.
    /// </summary>
    public static async Task ResumeHeldAsync(
        AppDbContext db, IReadOnlyCollection<string> subscriptionIds, DateTime now, string actor, CancellationToken ct = default)
    {
        if (subscriptionIds.Count == 0) return;
        var states = await db.SubscriptionBillingStates
            .Where(s => s.MembershipSubscriptionId != null
                     && subscriptionIds.Contains(s.MembershipSubscriptionId)
                     && s.Status == RecurringBillingStatus.Stopped
                     && s.LastFailureReason == ReasonMembershipOnHold)
            .ToListAsync(ct);
        foreach (var s in states)
        {
            if (s.NextBillingDate.Date < now.Date)
            {
                s.NextBillingDate   = now.Date;
                s.BillingAnchorDate = now.Date;
            }
            s.NextAttemptDate     = s.NextBillingDate;
            s.CurrentAttemptIndex = 0;
            s.Status              = RecurringBillingStatus.Active;
            s.LastFailureReason   = null;
            s.LastUpdateDate      = now;
            s.LastUpdateBy        = actor;
        }
    }
}
