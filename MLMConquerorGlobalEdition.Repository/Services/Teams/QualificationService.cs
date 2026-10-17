using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// «Calificado» en un único sitio (Plan J, corrección de Nick N3, sustituye a J15): la membresía
/// actual está activa Y además se cumple una de tres:
/// <list type="bullet">
///   <item>(a) se inscribió hace menos de 12 meses (hora de servidor, borde estricto);</item>
///   <item>(b) pagó su anualidad en curso (<see cref="IAnnualFeeStatus"/>; hoy nunca, Plan K);</item>
///   <item>(c) tiene la marca <c>MemberProfile.KeepQualified</c>.</item>
/// </list>
/// Membresía actual = la suscripción de StartDate más reciente (desempate por CreationDate), sea
/// cual sea su estado. Lo usan los reportes del Plan J, Mi equipo de inscripción y Dual Team.
/// </summary>
public interface IQualificationService
{
    Task<bool> IsQualifiedAsync(string memberId, CancellationToken ct = default);

    /// <summary>Una entrada por cada miembro pedido (false si no existe). Pensado para una página.</summary>
    Task<IReadOnlyDictionary<string, bool>> AreQualifiedAsync(
        IReadOnlyCollection<string> memberIds, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class QualificationService : IQualificationService
{
    public const int RecentEnrollmentMonths = 12;

    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _clock;
    private readonly IAnnualFeeStatus  _annualFee;

    public QualificationService(AppDbContext db, IDateTimeProvider clock, IAnnualFeeStatus annualFee)
    {
        _db        = db;
        _clock     = clock;
        _annualFee = annualFee;
    }

    public async Task<bool> IsQualifiedAsync(string memberId, CancellationToken ct = default) =>
        (await AreQualifiedAsync(new[] { memberId }, ct))[memberId];

    public async Task<IReadOnlyDictionary<string, bool>> AreQualifiedAsync(
        IReadOnlyCollection<string> memberIds, CancellationToken ct = default)
    {
        var ids = memberIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<string, bool>();

        var activeNow = (await _db.MembershipSubscriptions.AsNoTracking()
                .Where(s => ids.Contains(s.MemberId) && !s.IsDeleted)
                .Select(s => new { s.MemberId, s.SubscriptionStatus, s.StartDate, s.CreationDate })
                .ToListAsync(ct))
            .GroupBy(s => s.MemberId)
            .Where(g => g.OrderByDescending(s => s.StartDate).ThenByDescending(s => s.CreationDate)
                         .First().SubscriptionStatus == MembershipStatus.Active)
            .Select(g => g.Key)
            .ToHashSet();

        var profiles = await _db.MemberProfiles.AsNoTracking()
            .Where(p => ids.Contains(p.MemberId))
            .Select(p => new { p.MemberId, p.EnrollDate, p.KeepQualified })
            .ToDictionaryAsync(p => p.MemberId, ct);

        var recentSince = _clock.Now.AddMonths(-RecentEnrollmentMonths);
        var annualPaid  = await _annualFee.PaidCurrentAnnualFeeAsync(ids, ct);

        return ids.ToDictionary(
            id => id,
            id => activeNow.Contains(id)
                  && profiles.TryGetValue(id, out var p)
                  && (p.EnrollDate > recentSince || annualPaid.Contains(id) || p.KeepQualified));
    }
}
