using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

/// <summary>
/// Lee de la base los hechos del miembro que deciden su cuota (Plan K). Un solo sitio para Billing (cobrar)
/// y el BizCenter (enseñar), para que no diverjan.
/// </summary>
public static class AnnualFeeMemberQueries
{
    /// <summary>
    /// El perfil y el estado de su membresía vigente: en HoldByBilling (decisión de Nick, 2026-10-17) u OnHold (revisión
    /// de Q3, I4), o
    /// cancelada (Cancelled o con la cancelación programada cumplida; decisión de Nick, M-8). La vigente es
    /// la suscripción no borrada más reciente por StartDate y luego CreationDate, la misma regla que
    /// QualificationService. Sin perfil: <see cref="AnnualFeeMemberFacts.Missing"/>.
    /// </summary>
    /// <param name="now">Hora de servidor, para saber si una cancelación programada ya se cumplió.</param>
    public static async Task<AnnualFeeMemberFacts> FactsAsync(
        AppDbContext db, string memberId, DateTime now, CancellationToken ct = default)
    {
        var profile = await db.MemberProfiles.AsNoTracking().FirstOrDefaultAsync(m => m.MemberId == memberId, ct);
        if (profile is null || profile.IsDeleted) return AnnualFeeMemberFacts.Missing;

        var current = await db.MembershipSubscriptions.AsNoTracking()
            .Where(s => s.MemberId == memberId && !s.IsDeleted)
            .OrderByDescending(s => s.StartDate).ThenByDescending(s => s.CreationDate)
            .Select(s => new { s.SubscriptionStatus, s.IsAutoRenew, s.CancellationDate })
            .FirstOrDefaultAsync(ct);

        return AnnualFeeMemberFacts.Of(profile,
            // Revision de Q3 (I4): la retenida (OnHold, p. ej. por el admin al suspender o inactivar la cuenta) cuenta igual que
            // HoldByBilling: la cuota se pospone y "Pagar ahora" no se ofrece.
            membershipOnHold: current?.SubscriptionStatus is MembershipStatus.HoldByBilling or MembershipStatus.OnHold,
            membershipCancelled: current is not null
                && Billing.MembershipBillingStops.IsCancelled(current.SubscriptionStatus, current.IsAutoRenew, current.CancellationDate, now));
    }
}
