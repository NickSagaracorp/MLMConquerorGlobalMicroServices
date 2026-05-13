using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;

/// <summary>
/// Stage 3 of the high-volume pipeline (BILLING-RULES §10.5).
/// See <see cref="IUplineAggregator"/> for the full contract.
/// </summary>
public class UplineAggregator : IUplineAggregator
{
    private readonly AppDbContext                 _db;
    private readonly IDateTimeProvider            _dateTime;
    private readonly ILogger<UplineAggregator>    _logger;

    private const string Actor = "upline-aggregator";

    public UplineAggregator(
        AppDbContext db,
        IDateTimeProvider dateTime,
        ILogger<UplineAggregator> logger)
    {
        _db       = db;
        _dateTime = dateTime;
        _logger   = logger;
    }

    public async Task<Result<UplineAggregatorResult>> AggregateAsync(
        string batchId,
        CancellationToken ct = default)
    {
        // ── Load all Queued events for this batch ──────────────────────────────
        var queuedEvents = await _db.PointDeltaEvents
            .Where(e => e.BatchId == batchId && e.Status == PointDeltaEventStatus.Queued)
            .ToListAsync(ct);

        if (queuedEvents.Count == 0)
        {
            _logger.LogInformation(
                "UplineAggregator: no Queued PointDeltaEvents for batch {BatchId}.", batchId);
            return Result<UplineAggregatorResult>.Success(new UplineAggregatorResult
            {
                EventsApplied       = 0,
                UplineMembersUpdated = 0
            });
        }

        _logger.LogInformation(
            "UplineAggregator: {Count} Queued events for batch {BatchId}.", queuedEvents.Count, batchId);

        // ── Collect the unique downline member IDs ─────────────────────────────
        var downlineMemberIds = queuedEvents.Select(e => e.MemberId).Distinct().ToList();

        // ── Load each downline member's HierarchyPath (enrollment tree) ────────
        var hierarchyPaths = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => downlineMemberIds.Contains(g.MemberId))
            .Select(g => new { g.MemberId, g.HierarchyPath })
            .ToDictionaryAsync(g => g.MemberId, g => g.HierarchyPath, ct);

        // ── Build net-delta accumulator keyed by upline MemberId ──────────────
        // Key = upline member ID; Value = (DualTeam, Enrollment, Personal) net delta.
        var netDeltas = new Dictionary<string, (int DualTeam, int Enrollment, int Personal)>();

        foreach (var ev in queuedEvents)
        {
            if (!hierarchyPaths.TryGetValue(ev.MemberId, out var path) || string.IsNullOrEmpty(path))
            {
                _logger.LogWarning(
                    "UplineAggregator: no HierarchyPath for member {MemberId} — event {EventId} skipped.",
                    ev.MemberId, ev.Id);
                continue;
            }

            // HierarchyPath format: "/root/ancestor1/ancestor2/.../memberId/"
            // Every segment except the member's own ID is an upline to credit.
            var uplineIds = ParseUplineIdsFromPath(path, ev.MemberId);

            foreach (var uplineId in uplineIds)
            {
                if (!netDeltas.TryGetValue(uplineId, out var current))
                    current = (0, 0, 0);

                netDeltas[uplineId] = (
                    current.DualTeam   + ev.DualTeamDelta,
                    current.Enrollment + ev.EnrollmentDelta,
                    current.Personal   + 0  // PersonalDelta belongs to the member's own stats, not upline
                );
            }

            // Apply PersonalDelta to the member's own stats
            if (ev.PersonalDelta != 0)
            {
                if (!netDeltas.TryGetValue(ev.MemberId, out var selfCurrent))
                    selfCurrent = (0, 0, 0);

                netDeltas[ev.MemberId] = (
                    selfCurrent.DualTeam,
                    selfCurrent.Enrollment,
                    selfCurrent.Personal + ev.PersonalDelta
                );
            }
        }

        // ── Apply net deltas to MemberStatisticEntity ─────────────────────────
        // Load all affected stat rows in one query, then update in memory and save.
        var affectedMemberIds = netDeltas.Keys.ToList();

        var stats = await _db.MemberStatistics
            .Where(s => affectedMemberIds.Contains(s.MemberId))
            .ToListAsync(ct);

        var now = _dateTime.Now;
        int updatedCount = 0;

        foreach (var statRow in stats)
        {
            if (!netDeltas.TryGetValue(statRow.MemberId, out var delta)) continue;

            statRow.DualTeamPoints   += delta.DualTeam;
            statRow.EnrollmentPoints += delta.Enrollment;
            statRow.PersonalPoints   += delta.Personal;
        }

        // For any member IDs that have no stat row yet, we log rather than silently skip —
        // stat rows should be created at enrollment time. Missing rows here indicate a
        // seeder or enrollment bug, not an aggregator bug.
        var missingStatIds = affectedMemberIds
            .Except(stats.Select(s => s.MemberId))
            .ToList();

        if (missingStatIds.Count > 0)
        {
            _logger.LogWarning(
                "UplineAggregator: {Count} upline members have no MemberStatisticEntity row — " +
                "deltas cannot be applied: {Ids}",
                missingStatIds.Count,
                string.Join(", ", missingStatIds.Take(20)));
        }

        // ── Mark events Applied (in same save) ────────────────────────────────
        foreach (var ev in queuedEvents)
        {
            ev.Status   = PointDeltaEventStatus.Applied;
            ev.AppliedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        updatedCount = stats.Count;

        _logger.LogInformation(
            "UplineAggregator: batch {BatchId} complete — {Events} events applied, {Members} upline rows updated.",
            batchId, queuedEvents.Count, updatedCount);

        return Result<UplineAggregatorResult>.Success(new UplineAggregatorResult
        {
            EventsApplied        = queuedEvents.Count,
            UplineMembersUpdated = updatedCount
        });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a HierarchyPath of the form "/root/id1/id2/.../memberId/" and returns
    /// all segment IDs except the member's own ID (those are the upline ancestors).
    /// </summary>
    private static IEnumerable<string> ParseUplineIdsFromPath(string hierarchyPath, string memberId)
    {
        // Strip leading/trailing slashes; split on slash.
        var segments = hierarchyPath.Trim('/').Split('/');

        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment)) continue;
            if (segment.Equals(memberId, StringComparison.OrdinalIgnoreCase)) continue;
            yield return segment;
        }
    }
}
