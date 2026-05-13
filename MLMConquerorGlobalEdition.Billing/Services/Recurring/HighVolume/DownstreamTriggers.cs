using Hangfire;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;

/// <summary>
/// Stage 4 of the high-volume pipeline (BILLING-RULES §10.6).
/// See <see cref="IDownstreamTriggers"/> for the full contract.
/// </summary>
public class DownstreamTriggers : IDownstreamTriggers
{
    private readonly AppDbContext                  _db;
    private readonly IDateTimeProvider             _dateTime;
    private readonly IPushNotificationService      _push;
    private readonly IBackgroundJobClient          _hangfire;
    private readonly ILogger<DownstreamTriggers>   _logger;

    private const string Actor = "downstream-triggers";

    public DownstreamTriggers(
        AppDbContext db,
        IDateTimeProvider dateTime,
        IPushNotificationService push,
        IBackgroundJobClient hangfire,
        ILogger<DownstreamTriggers> logger)
    {
        _db       = db;
        _dateTime = dateTime;
        _push     = push;
        _hangfire = hangfire;
        _logger   = logger;
    }

    public async Task<Result<DownstreamTriggersResult>> DispatchAsync(
        string batchId,
        CancellationToken ct = default)
    {
        var now = _dateTime.Now;
        int rankEntries = 0, commTriggers = 0, pushNotifs = 0;

        // ──────────────────────────────────────────────────────────────────────
        // 1. Rank re-evaluation
        //    For every upline member affected by Applied PointDeltaEvents in this
        //    batch, enqueue a RankEvaluationQueue entry.
        // ──────────────────────────────────────────────────────────────────────

        var appliedEvents = await _db.PointDeltaEvents
            .AsNoTracking()
            .Where(e => e.BatchId == batchId && e.Status == PointDeltaEventStatus.Applied)
            .Select(e => e.MemberId)
            .Distinct()
            .ToListAsync(ct);

        if (appliedEvents.Count > 0)
        {
            // Collect all upline members for all activated downline members.
            // We use the GenealogyTree HierarchyPath to extract upline IDs.
            var downlinePaths = await _db.GenealogyTree
                .AsNoTracking()
                .Where(g => appliedEvents.Contains(g.MemberId))
                .Select(g => new { g.MemberId, g.HierarchyPath })
                .ToListAsync(ct);

            // Build a distinct set of (triggerMemberId → evaluateMemberId) pairs
            var rankQueue = new List<(string TriggerMemberId, string EvaluateMemberId)>();

            foreach (var row in downlinePaths)
            {
                var uplineIds = ParseUplineIds(row.HierarchyPath, row.MemberId);
                foreach (var uplineId in uplineIds)
                    rankQueue.Add((row.MemberId, uplineId));
            }

            foreach (var (triggerMemberId, evaluateMemberId) in rankQueue.Distinct())
            {
                _db.RankEvaluationQueue.Add(new RankEvaluationQueue
                {
                    TriggerMemberId  = triggerMemberId,
                    EvaluateMemberId = evaluateMemberId,
                    TriggerEvent     = RankEvaluationTrigger.MembershipChange,
                    TriggerDate      = now,
                    IsProcessed      = false,
                    CreatedBy        = Actor,
                    CreationDate     = now
                });
                rankEntries++;
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "DownstreamTriggers: batch {BatchId} — {Count} rank evaluation entries enqueued.",
                batchId, rankEntries);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 2. Commission triggers
        //    Process unprocessed CommissionTriggerQueue rows; dispatch one
        //    Hangfire job per trigger into the "commissions" queue.
        // ──────────────────────────────────────────────────────────────────────

        var pendingTriggers = await _db.CommissionTriggerQueues
            .Where(t => t.BatchId == batchId && !t.IsProcessed)
            .ToListAsync(ct);

        foreach (var trigger in pendingTriggers)
        {
            try
            {
                // Enqueue to the "commissions" queue (processed by CommissionEngine server).
                // The job ID is structured so it can be de-duplicated at the Hangfire level.
                _hangfire.Enqueue<ICommissionTriggerDispatcher>(
                    d => d.DispatchAsync(trigger.MemberId, trigger.OrderId, trigger.TriggerType,
                        CancellationToken.None));

                trigger.IsProcessed  = true;
                trigger.ProcessedAt  = now;
                commTriggers++;
            }
            catch (Exception ex)
            {
                trigger.ErrorMessage = ex.Message;
                _logger.LogError(ex,
                    "DownstreamTriggers: failed to enqueue commission trigger for member {MemberId} / type {Type}.",
                    trigger.MemberId, trigger.TriggerType);
            }
        }

        if (pendingTriggers.Count > 0)
            await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "DownstreamTriggers: batch {BatchId} — {Count} commission triggers dispatched.",
            batchId, commTriggers);

        // ──────────────────────────────────────────────────────────────────────
        // 3. Push notifications — renewal success for Activated members
        // ──────────────────────────────────────────────────────────────────────

        var activatedMemberIds = await _db.PointDeltaEvents
            .AsNoTracking()
            .Where(e => e.BatchId == batchId
                     && e.EventType == PointDeltaEventType.Activated
                     && e.Status == PointDeltaEventStatus.Applied)
            .Select(e => e.MemberId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var memberId in activatedMemberIds)
        {
            try
            {
                await _push.SendAsync(
                    memberId,
                    "MEMBERSHIP_RENEWED",
                    "Membership Renewed",
                    "Your membership has been successfully renewed. Thank you!",
                    ct);
                pushNotifs++;
            }
            catch (Exception ex)
            {
                // IPushNotificationService contract: implementations must never throw.
                // If one does, we log and continue — do not abort the batch.
                _logger.LogError(ex,
                    "DownstreamTriggers: push notification failed for member {MemberId}.", memberId);
            }
        }

        _logger.LogInformation(
            "DownstreamTriggers: batch {BatchId} — {Count} push notifications enqueued.",
            batchId, pushNotifs);

        return Result<DownstreamTriggersResult>.Success(new DownstreamTriggersResult
        {
            RankQueueEntriesAdded       = rankEntries,
            CommissionTriggersProcessed = commTriggers,
            PushNotificationsEnqueued   = pushNotifs
        });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static IEnumerable<string> ParseUplineIds(string hierarchyPath, string memberId)
    {
        var segments = hierarchyPath.Trim('/').Split('/');
        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment)) continue;
            if (segment.Equals(memberId, StringComparison.OrdinalIgnoreCase)) continue;
            yield return segment;
        }
    }
}
