using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.BizCenter.Jobs;

/// <summary>
/// HangFire recurring job — Nightly 1:00 AM UTC.
/// Refreshes QualifiedSponsoredMembers in MemberStatisticEntity by counting
/// active subscriptions grouped by the sponsor (MemberProfile.SponsorMemberId).
/// Idempotent: re-running produces the same result.
/// </summary>
public class MemberStatisticSnapshotJob
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<MemberStatisticSnapshotJob> _logger;

    public MemberStatisticSnapshotJob(
        AppDbContext db,
        IDateTimeProvider dateTime,
        ILogger<MemberStatisticSnapshotJob> logger)
    {
        _db       = db;
        _dateTime = dateTime;
        _logger   = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now = _dateTime.Now;
        _logger.LogInformation("MemberStatisticSnapshotJob: starting at {Now}.", now);

        // Count active sponsored members per sponsor from live data
        var sponsorCounts = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.SponsorMemberId != null
                     && m.Status == Domain.Entities.Member.MemberAccountStatus.Active)
            .GroupBy(m => m.SponsorMemberId!)
            .Select(g => new { SponsorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SponsorId, x => x.Count, ct);

        var stats = await _db.MemberStatistics.ToListAsync(ct);
        var updated = 0;

        foreach (var stat in stats)
        {
            var count = sponsorCounts.GetValueOrDefault(stat.MemberId, 0);
            if (stat.QualifiedSponsoredMembers != count)
            {
                stat.QualifiedSponsoredMembers = count;
                updated++;
            }
        }

        if (updated > 0)
            await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "MemberStatisticSnapshotJob: completed — {Updated} records updated at {Now}.",
            updated, now);
    }
}
