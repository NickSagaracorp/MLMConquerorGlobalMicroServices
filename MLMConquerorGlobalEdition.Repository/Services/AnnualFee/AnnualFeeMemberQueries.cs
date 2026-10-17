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
    /// El perfil y si su membresía vigente está en HoldByBilling (decisión de Nick, 2026-10-17). La
    /// vigente es la suscripción no borrada más reciente por StartDate y luego CreationDate, la misma
    /// regla que QualificationService. Sin perfil: <see cref="AnnualFeeMemberFacts.Missing"/>.
    /// </summary>
    public static async Task<AnnualFeeMemberFacts> FactsAsync(AppDbContext db, string memberId, CancellationToken ct = default)
    {
        var profile = await db.MemberProfiles.AsNoTracking().FirstOrDefaultAsync(m => m.MemberId == memberId, ct);
        if (profile is null || profile.IsDeleted) return AnnualFeeMemberFacts.Missing;

        var current = await db.MembershipSubscriptions.AsNoTracking()
            .Where(s => s.MemberId == memberId && !s.IsDeleted)
            .OrderByDescending(s => s.StartDate).ThenByDescending(s => s.CreationDate)
            .Select(s => (MembershipStatus?)s.SubscriptionStatus)
            .FirstOrDefaultAsync(ct);

        return AnnualFeeMemberFacts.Of(profile, membershipOnHold: current == MembershipStatus.HoldByBilling);
    }
}
