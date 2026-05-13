using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.BizCenter.Jobs;

/// <summary>
/// HangFire recurring job — Nightly 1:00 AM UTC.
///
/// Responsibilities:
///   1. Refresh QualifiedSponsoredMembers in MemberStatisticEntity from live
///      MemberProfile data so the live counter never drifts.
///   2. Upsert a row into MemberStatisticHistory for the current calendar
///      month per member, mirroring the live MemberStatisticEntity values
///      and joining the L/R leg points from DualTeamTree. Running multiple
///      times in the same month overwrites the same row — we always keep
///      the latest within-month snapshot, which is what the residuals chart
///      and any other 6-month trend needs.
///
/// Idempotent: re-running at any point in the same nightly window produces
/// the same end state.
/// </summary>
[Queue("bizcenter")]
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

        // ── 1. Refresh QualifiedSponsoredMembers ────────────────────────────
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

        // ── 2. Upsert monthly history snapshot ──────────────────────────────
        var year  = now.Year;
        var month = now.Month;

        var dualNodes = await _db.DualTeamTree.AsNoTracking()
            .Select(d => new { d.MemberId, d.LeftLegPoints, d.RightLegPoints })
            .ToDictionaryAsync(d => d.MemberId, d => d, ct);

        // Pull existing rows for the current month into memory once and
        // upsert in a single SaveChanges so the SQL round-trip count stays
        // proportional to the # of members not 2x.
        var existingRows = await _db.MemberStatisticHistories
            .Where(h => h.SnapshotYear == year && h.SnapshotMonth == month)
            .ToListAsync(ct);
        var existingMap = existingRows.ToDictionary(h => h.MemberId);

        var inserted = 0;
        var refreshed = 0;
        foreach (var stat in stats)
        {
            dualNodes.TryGetValue(stat.MemberId, out var dual);
            var leftLeg  = dual?.LeftLegPoints  ?? 0m;
            var rightLeg = dual?.RightLegPoints ?? 0m;

            if (existingMap.TryGetValue(stat.MemberId, out var row))
            {
                // AuditChangesLongKey is the high-volume audit base — only
                // CreationDate/CreatedBy. Mid-month rewrites overwrite the
                // value columns in place; the snapshot is a "latest within
                // month" semantic, not an append-only log.
                CopyStatToHistory(stat, row, leftLeg, rightLeg);
                refreshed++;
            }
            else
            {
                var newRow = new MemberStatisticHistoryEntity
                {
                    MemberId       = stat.MemberId,
                    SnapshotYear   = year,
                    SnapshotMonth  = month,
                    CreationDate   = now,
                    CreatedBy      = "snapshot-job"
                };
                CopyStatToHistory(stat, newRow, leftLeg, rightLeg);
                _db.MemberStatisticHistories.Add(newRow);
                inserted++;
            }
        }

        if (inserted > 0 || refreshed > 0)
            await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "MemberStatisticSnapshotJob: completed — sponsor counts updated: {Updated}; history rows: +{Inserted} new, {Refreshed} refreshed for {Year}-{Month:00} at {Now}.",
            updated, inserted, refreshed, year, month, now);
    }

    private static void CopyStatToHistory(
        MemberStatisticEntity stat,
        MemberStatisticHistoryEntity row,
        decimal leftLeg, decimal rightLeg)
    {
        row.PersonalPoints                      = stat.PersonalPoints;
        row.ExternalCustomerPoints              = stat.ExternalCustomerPoints;
        row.DualTeamSize                        = stat.DualTeamSize;
        row.EnrollmentTeamSize                  = stat.EnrollmentTeamSize;
        row.DualTeamPoints                      = stat.DualTeamPoints;
        row.EnrollmentPoints                    = stat.EnrollmentPoints;
        row.QualifiedSponsoredMembers           = stat.QualifiedSponsoredMembers;
        row.QualifiedSponsoredExternalCustomers = stat.QualifiedSponsoredExternalCustomers;
        row.EnrollmentTeamGrowth                = stat.EnrollmentTeamGrowth;
        row.DualteamGrowth                      = stat.DualteamGrowth;
        row.EnrollmentTeamPointsGrowth          = stat.EnrollmentTeamPointsGrowth;
        row.DualTeamPointsGrowth                = stat.DualTeamPointsGrowth;
        row.CurrentWeekIncomeGrowth             = stat.CurrentWeekIncomeGrowth;
        row.CurrentMonthIncomeGrowth            = stat.CurrentMonthIncomeGrowth;
        row.CurrentYearIncomeGrowth             = stat.CurrentYearIncomeGrowth;
        row.LeftLegPoints                       = leftLeg;
        row.RightLegPoints                      = rightLeg;
    }
}
