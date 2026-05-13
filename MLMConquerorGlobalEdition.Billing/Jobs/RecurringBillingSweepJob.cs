using Hangfire;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// HangFire recurring job — Daily 7:00 AM UTC, queue "billing".
///
/// Acts as the safety-net fallback for the high-volume pipeline.
///
/// Responsibilities:
/// 1. Lazily ensure SubscriptionBillingState for any subscription whose product is governed
///    by an active RecurringBillingPlan (safety net — normally EnsureState is called on signup).
/// 2. Find all states with Status ∈ {Active, Retrying, AwaitingAnniversaryRetry}
///    and NextAttemptDate &lt;= today, and invoke the processor for each.
///    States already covered by a RecurringBillingBatchShard for today are skipped
///    to avoid double-processing with the high-volume pipeline (§10.7 skip guard).
/// Idempotent: the per-state NextAttemptDate prevents re-processing within a day.
/// </summary>
[Queue("billing")]
public class RecurringBillingSweepJob
{
    private readonly AppDbContext _db;
    private readonly IRecurringBillingProcessor _processor;
    private readonly IRecurringBillingEnrollmentService _enrollment;
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<RecurringBillingSweepJob> _logger;

    public RecurringBillingSweepJob(
        AppDbContext db,
        IRecurringBillingProcessor processor,
        IRecurringBillingEnrollmentService enrollment,
        IDateTimeProvider dateTime,
        ILogger<RecurringBillingSweepJob> logger)
    {
        _db         = db;
        _processor  = processor;
        _enrollment = enrollment;
        _dateTime   = dateTime;
        _logger     = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var today = _dateTime.Now.Date;
        _logger.LogInformation("RecurringBillingSweepJob: starting sweep for {Date}.", today);

        // ── Phase 1: Lazily create missing SubscriptionBillingState rows ────────
        await EnsureMissingStatesAsync(today, ct);

        // ── Phase 2: Process all due states ──────────────────────────────────────
        // Skip states already covered by a RecurringBillingBatchShard for today
        // (they are processed by the high-volume pipeline workers).
        var coveredShardRanges = await _db.RecurringBillingBatchShards
            .AsNoTracking()
            .Where(s => !s.IsDeleted
                     && _db.RecurringBillingBatches.Any(b => b.Id == s.BatchId
                            && b.RunDate.Date == today && !b.IsDeleted))
            .Select(s => new { s.IdRangeStart, s.IdRangeEnd })
            .ToListAsync(ct);

        var dueStateIds = await _db.SubscriptionBillingStates
            .AsNoTracking()
            .Where(s => (s.Status == RecurringBillingStatus.Active
                      || s.Status == RecurringBillingStatus.Retrying
                      || s.Status == RecurringBillingStatus.AwaitingAnniversaryRetry)
                     && s.NextAttemptDate.Date <= today)
            .Select(s => new { s.Id, s.ShardKey })
            .ToListAsync(ct);

        // Filter out states whose ShardKey falls within any covered shard range.
        var dueStateIdFiltered = coveredShardRanges.Count == 0
            ? dueStateIds.Select(s => s.Id).ToList()
            : dueStateIds
                .Where(s => !coveredShardRanges.Any(r => s.ShardKey >= r.IdRangeStart && s.ShardKey <= r.IdRangeEnd))
                .Select(s => s.Id)
                .ToList();

        _logger.LogInformation(
            "RecurringBillingSweepJob: {Total} due states found; {Covered} covered by high-volume shards; {ToProcess} to process.",
            dueStateIds.Count, dueStateIds.Count - dueStateIdFiltered.Count, dueStateIdFiltered.Count);

        int succeeded = 0, failed = 0, skipped = 0;

        foreach (var stateId in dueStateIdFiltered)
        {
            try
            {
                var result = await _processor.ProcessAsync(stateId, forceBillNow: false, ct);
                if (result.IsSuccess)
                {
                    switch (result.Value!.Outcome)
                    {
                        case "Success":   succeeded++; break;
                        case "Skipped":   skipped++;   break;
                        case "Scheduled": skipped++;   break;
                        default:          failed++;    break;
                    }
                    _logger.LogInformation(
                        "RecurringBillingSweepJob: state {StateId} — {Outcome} ({Funding}).",
                        stateId, result.Value.Outcome, result.Value.FundingSource ?? "n/a");
                }
                else
                {
                    failed++;
                    _logger.LogWarning(
                        "RecurringBillingSweepJob: state {StateId} — processor error [{Code}]: {Error}",
                        stateId, result.ErrorCode, result.Error);
                }
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex,
                    "RecurringBillingSweepJob: unhandled exception processing state {StateId}.", stateId);
            }
        }

        _logger.LogInformation(
            "RecurringBillingSweepJob: completed — Success={Succeeded}, Failed={Failed}, Skipped={Skipped}.",
            succeeded, failed, skipped);
    }

    // ── Lazy state creation ────────────────────────────────────────────────────

    private async Task EnsureMissingStatesAsync(DateTime today, CancellationToken ct)
    {
        // Find active subscriptions linked to recurring-plan products that have no state row yet.
        // Join: Subscription → MembershipLevel → Product → RecurringBillingPlanProduct
        var subscriptionsNeedingState = await (
            from sub in _db.MembershipSubscriptions
            join level in _db.MembershipLevels on sub.MembershipLevelId equals level.Id
            join prod in _db.Products on level.Id equals prod.MembershipLevelId
            join planProd in _db.RecurringBillingPlanProducts on prod.Id equals planProd.ProductId
            join plan in _db.RecurringBillingPlans on planProd.RecurringBillingPlanId equals plan.Id
            where sub.SubscriptionStatus == Domain.Entities.Membership.MembershipStatus.Active
               && !sub.IsDeleted
               && plan.IsActive
               && !_db.SubscriptionBillingStates.Any(s => s.MembershipSubscriptionId == sub.Id)
            select sub
        ).ToListAsync(ct);

        if (subscriptionsNeedingState.Count == 0)
            return;

        _logger.LogInformation(
            "RecurringBillingSweepJob: lazily creating {Count} missing billing states.",
            subscriptionsNeedingState.Count);

        foreach (var sub in subscriptionsNeedingState)
        {
            try
            {
                // Load the MembershipLevel navigation if needed
                if (sub.MembershipLevel is null)
                    await _db.Entry(sub).Reference(s => s.MembershipLevel).LoadAsync(ct);

                await _enrollment.EnsureStateForSubscriptionAsync(sub, "recurring-billing-sweep", ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "RecurringBillingSweepJob: failed to create billing state for subscription {SubId}.",
                    sub.Id);
            }
        }
    }
}
