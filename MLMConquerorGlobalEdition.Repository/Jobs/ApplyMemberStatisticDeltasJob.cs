using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Jobs;

/// <summary>
/// Sprint-16 — drains <c>MemberStatisticDeltas</c> into <c>MemberStatistics</c>.
///
/// CompleteSignupHandler enqueues one delta row per ancestor (76 rows for a
/// 76-deep tree) in a single batch insert instead of issuing 76 MERGE
/// round-trips inline. This job runs every minute on the <c>signups</c> queue,
/// claims up to <see cref="BatchSize"/> unapplied rows per inner batch and loops
/// until the whole backlog is drained, groups by <c>MemberId</c> (so N signups
/// under the same upline collapse to one MERGE per cycle), and applies the summed
/// deltas with a race-free MERGE WITH (HOLDLOCK). It then re-enqueues a
/// <c>RankEvaluationQueue</c> row per touched member so the rank processor sees
/// fresh stats on its next tick.
///
/// IMPORTANT — this type lives in the shared Repository assembly ON PURPOSE.
/// Hangfire's RecurringJobScheduler runs on EVERY service's background server and
/// must deserialize the recurring job's type to schedule it. When this class lived
/// in MLMConquerorGlobalEdition.SignupAPI, sibling services (Billing, BizCenter,
/// CommissionEngine, RankEngine, TicketManagementSystem) could not resolve the
/// assembly → JobLoadException → after 5 retries the recurring entry was poisoned
/// (NextExecution=null) and auto-draining silently stopped. Every Hangfire server
/// references Repository, so keeping the job here makes the type loadable by all
/// schedulers. Execution still happens only on the SignupAPI server because the
/// recurring job is enqueued onto the <c>signups</c> queue, which only it listens on.
/// </summary>
[Queue("signups")]
public class ApplyMemberStatisticDeltasJob
{
    /// <summary>Rows claimed per inner batch. Each cycle loops batches until the queue is
    /// drained (see ExecuteAsync), so this just bounds the per-transaction size. Sized for
    /// bulk signup load (a rank-climb can enqueue hundreds of thousands of deltas; 1000/min
    /// could never catch up).</summary>
    public const int BatchSize = 5000;

    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<ApplyMemberStatisticDeltasJob> _logger;

    public ApplyMemberStatisticDeltasJob(
        AppDbContext db,
        IDateTimeProvider dateTime,
        ILogger<ApplyMemberStatisticDeltasJob> logger)
    {
        _db       = db;
        _dateTime = dateTime;
        _logger   = logger;
    }

    // Prevent the 1-minute recurrence from starting a second drainer while a long backlog
    // drain is still running — overlapping runs would claim the same rows (no row lock) and
    // double-apply the deltas. One drainer at a time is correct and sufficient.
    [DisableConcurrentExecution(timeoutInSeconds: 7200)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var startedAt    = _dateTime.Now;
        var providerName = _db.Database.ProviderName ?? string.Empty;
        var isInMemory   = providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase);
        var totalApplied = 0;

        // Drain the whole backlog this run, one BatchSize transaction at a time, until no
        // unapplied rows remain.
        while (true)
        {
        // 1. Claim a batch of unapplied rows. The [DisableConcurrentExecution] guard
        //    guarantees a single drainer, so no two workers claim the same rows.
        var batch = await _db.MemberStatisticDeltas
            .Where(d => !d.IsApplied)
            .OrderBy(d => d.Id)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (batch.Count == 0) break;

        // 2. Group by upline and sum deltas — 350 signups under the same upline produce
        //    350 delta rows but only one MERGE this cycle.
        var groups = batch
            .GroupBy(d => d.MemberId)
            .Select(g => new
            {
                MemberId                       = g.Key,
                EnrollmentPointsDelta          = g.Sum(x => x.EnrollmentPointsDelta),
                EnrollmentTeamSizeDelta        = g.Sum(x => x.EnrollmentTeamSizeDelta),
                QualifiedSponsoredMembersDelta = g.Sum(x => x.QualifiedSponsoredMembersDelta),
                EarliestCreatedBy              = g.OrderBy(x => x.Id).First().CreatedBy
            })
            .ToList();

        var now = _dateTime.Now;

        // 3. Apply each group. On SQL Server: race-free MERGE per upline (HOLDLOCK). On the
        //    in-memory provider (unit tests): read-modify-write — no concurrency there.
        foreach (var grp in groups)
        {
            if (isInMemory)
            {
                var existing = await _db.MemberStatistics
                    .FirstOrDefaultAsync(s => s.MemberId == grp.MemberId, ct);

                if (existing is not null)
                {
                    existing.EnrollmentPoints          += grp.EnrollmentPointsDelta;
                    existing.EnrollmentTeamSize        += grp.EnrollmentTeamSizeDelta;
                    existing.QualifiedSponsoredMembers += grp.QualifiedSponsoredMembersDelta;
                }
                else
                {
                    await _db.MemberStatistics.AddAsync(new MemberStatisticEntity
                    {
                        MemberId                  = grp.MemberId,
                        EnrollmentPoints          = grp.EnrollmentPointsDelta,
                        EnrollmentTeamSize        = grp.EnrollmentTeamSizeDelta,
                        QualifiedSponsoredMembers = grp.QualifiedSponsoredMembersDelta,
                        CreatedBy                 = grp.EarliestCreatedBy,
                        CreationDate              = now
                    }, ct);
                }
            }
            else
            {
                FormattableString mergeSql = $@"
MERGE INTO MemberStatistics WITH (HOLDLOCK) AS target
USING (SELECT {grp.MemberId} AS MemberId) AS source
   ON target.MemberId = source.MemberId
WHEN MATCHED THEN
    UPDATE SET
        EnrollmentPoints          = target.EnrollmentPoints          + {grp.EnrollmentPointsDelta},
        EnrollmentTeamSize        = target.EnrollmentTeamSize        + {grp.EnrollmentTeamSizeDelta},
        QualifiedSponsoredMembers = target.QualifiedSponsoredMembers + {grp.QualifiedSponsoredMembersDelta}
WHEN NOT MATCHED THEN
    INSERT (MemberId, PersonalPoints, ExternalCustomerPoints, DualTeamSize,
            EnrollmentTeamSize, DualTeamPoints, EnrollmentPoints,
            QualifiedSponsoredMembers, QualifiedSponsoredExternalCustomers,
            EnrollmentTeamGrowth, DualteamGrowth, EnrollmentTeamPointsGrowth,
            DualTeamPointsGrowth, CurrentWeekIncomeGrowth, CurrentMonthIncomeGrowth,
            CurrentYearIncomeGrowth, CreationDate, CreatedBy)
    VALUES (source.MemberId, 0, 0, 0,
            {grp.EnrollmentTeamSizeDelta}, 0, {grp.EnrollmentPointsDelta},
            {grp.QualifiedSponsoredMembersDelta}, 0,
            0, 0, 0,
            0, 0, 0,
            0, {now}, {grp.EarliestCreatedBy});";

                await _db.Database.ExecuteSqlInterpolatedAsync(mergeSql, ct);
            }
        }

        // 4. Mark all claimed rows applied.
        foreach (var delta in batch)
        {
            delta.IsApplied = true;
            delta.AppliedAt = now;
        }

        // 5. For each touched member, queue a rank evaluation entry so the rank processor
        //    sees fresh stats. Dedup against any unprocessed Enrollment row already in flight.
        var touchedMemberIds = groups.Select(g => g.MemberId).ToList();
        var alreadyQueued = await _db.RankEvaluationQueue
            .Where(q => !q.IsProcessed
                     && q.TriggerEvent == RankEvaluationTrigger.Enrollment
                     && touchedMemberIds.Contains(q.EvaluateMemberId))
            .Select(q => q.EvaluateMemberId)
            .ToListAsync(ct);
        var alreadyQueuedSet = new HashSet<string>(alreadyQueued, StringComparer.Ordinal);

        foreach (var grp in groups)
        {
            if (alreadyQueuedSet.Contains(grp.MemberId)) continue;

            await _db.RankEvaluationQueue.AddAsync(new RankEvaluationQueue
            {
                TriggerMemberId  = grp.MemberId,
                EvaluateMemberId = grp.MemberId,
                TriggerEvent     = RankEvaluationTrigger.Enrollment,
                TriggerDate      = now,
                CreatedBy        = "delta-apply",
                CreationDate     = now
            }, ct);
        }

        await _db.SaveChangesAsync(ct);

        totalApplied += batch.Count;
        _db.ChangeTracker.Clear(); // don't accumulate tracked entities across batches
        }

        var elapsedMs = (int)(_dateTime.Now - startedAt).TotalMilliseconds;
        if (totalApplied > 0)
            _logger.LogInformation(
                "ApplyMemberStatisticDeltas: applied {DeltaCount} deltas in {ElapsedMs}ms.",
                totalApplied, elapsedMs);
    }
}
