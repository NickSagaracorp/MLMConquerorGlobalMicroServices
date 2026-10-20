using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Billing;

/// <summary>
/// Plan Q (Q3, decidido por Nick: «si la cuenta se suspende, termina o inactiva desde admin tambien afecta los puntos»):
/// lo que el cambio de estado de la cuenta desde el admin hace con sus membresias. El interceptor (MembershipPointsDeltas)
/// resta los puntos al salir de Active y los suma al volver.
/// <list type="bullet">
/// <item>Suspended o Inactive: <see cref="HoldAsync"/>, sus activas pasan a OnHold con la marca
/// <see cref="MembershipSubscription.HeldByAccountStatus"/> y su cobro se para.</item>
/// <item>Terminated: <see cref="CancelForTerminationAsync"/>, sus activas, las que retuvo el admin, las retenidas por el
/// cobro (HoldByBilling) y las caducadas sin reemplazo pasan a Cancelled y su cobro se para.</item>
/// <item>De vuelta a Active: <see cref="RestoreAsync"/>, solo las OnHold con la marca vuelven a Active y su cobro se
/// reanuda. Una OnHold de otro origen (sin marca) se queda como esta.</item>
/// </list>
/// Quien llama toma antes el candado de puntos del miembro (<see cref="Teams.MemberPointsLocks"/>) y guarda: aqui no se
/// guarda. Devuelven cuantas suscripciones cambiaron.
/// </summary>
public static class AccountStatusMemberships
{
    public static async Task<int> HoldAsync(
        AppDbContext db, string memberId, MemberAccountStatus accountStatus, DateTime now, string actor,
        CancellationToken ct = default)
    {
        var activas = await db.MembershipSubscriptions
            .Where(s => s.MemberId == memberId && !s.IsDeleted && s.SubscriptionStatus == MembershipStatus.Active)
            .ToListAsync(ct);
        foreach (var s in activas)
        {
            s.SubscriptionStatus  = MembershipStatus.OnHold;
            s.HoldDate            = now;
            s.HeldByAccountStatus = accountStatus;
            s.LastUpdateDate      = now;
            s.LastUpdateBy        = actor;
        }
        await MembershipBillingStops.StopForSubscriptionsAsync(db, activas.Select(s => s.Id).ToList(),
            MembershipBillingStops.ReasonMembershipOnHold, now, actor, ct);
        return activas.Count;
    }

    public static async Task<int> CancelForTerminationAsync(
        AppDbContext db, string memberId, DateTime now, string actor, CancellationToken ct = default)
    {
        // Revision de Q3 (I3): tambien la retenida por el cobro (HoldByBilling) y la caducada sin reemplazo, que el motor
        // podia cobrar y reactivar en una cuenta terminada. La caducada reemplazada por una subida o bajada ya no se cobra
        // ni se reactiva: se queda como esta.
        var candidatas = await db.MembershipSubscriptions
            .Where(s => s.MemberId == memberId && !s.IsDeleted
                     && (s.SubscriptionStatus == MembershipStatus.Active
                         || s.SubscriptionStatus == MembershipStatus.HoldByBilling
                         || s.SubscriptionStatus == MembershipStatus.Expired
                         || (s.SubscriptionStatus == MembershipStatus.OnHold && s.HeldByAccountStatus != null)))
            .ToListAsync(ct);
        var vivas = new List<MembershipSubscription>(candidatas.Count);
        foreach (var s in candidatas)
            if (!await MembershipBillingStops.IsReplacedAsync(db, s, ct))
                vivas.Add(s);
        foreach (var s in vivas)
        {
            s.SubscriptionStatus  = MembershipStatus.Cancelled;
            s.CancellationDate    = now;
            s.EndDate             = now;
            s.IsAutoRenew         = false;
            s.HeldByAccountStatus = null;
            s.LastUpdateDate      = now;
            s.LastUpdateBy        = actor;
        }
        await MembershipBillingStops.StopForSubscriptionsAsync(db, vivas.Select(s => s.Id).ToList(),
            MembershipBillingStops.ReasonMembershipCancelled, now, actor, ct);
        return vivas.Count;
    }

    public static async Task<int> RestoreAsync(
        AppDbContext db, string memberId, DateTime now, string actor, CancellationToken ct = default)
    {
        var retenidas = await db.MembershipSubscriptions
            .Where(s => s.MemberId == memberId && !s.IsDeleted
                     && s.SubscriptionStatus == MembershipStatus.OnHold && s.HeldByAccountStatus != null)
            .ToListAsync(ct);
        foreach (var s in retenidas)
        {
            s.SubscriptionStatus  = MembershipStatus.Active;
            s.HoldDate            = null;
            s.HeldByAccountStatus = null;
            s.LastUpdateDate      = now;
            s.LastUpdateBy        = actor;
        }
        await MembershipBillingStops.ResumeHeldAsync(db, retenidas.Select(s => s.Id).ToList(), now, actor, ct);
        return retenidas.Count;
    }
}
