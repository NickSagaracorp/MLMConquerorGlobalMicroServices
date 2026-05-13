using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;

/// <summary>
/// Stage 2 of the high-volume pipeline (BILLING-RULES §10.4).
/// See <see cref="IRecurringChargeWorker"/> for the full contract.
/// </summary>
public class RecurringChargeWorker : IRecurringChargeWorker
{
    private readonly AppDbContext                    _db;
    private readonly IRecurringBillingProcessor      _processor;
    private readonly IDateTimeProvider               _dateTime;
    private readonly ILogger<RecurringChargeWorker>  _logger;

    private const string Actor = "recurring-charge-worker";

    // Commission types whose triggers must be propagated to CommissionEngine after a renewal.
    private static readonly string[] CommissionTriggerTypes = { "FastStartBonus", "BoostBonus" };

    public RecurringChargeWorker(
        AppDbContext db,
        IRecurringBillingProcessor processor,
        IDateTimeProvider dateTime,
        ILogger<RecurringChargeWorker> logger)
    {
        _db        = db;
        _processor = processor;
        _dateTime  = dateTime;
        _logger    = logger;
    }

    public async Task<Result<RecurringChargeWorkerResult>> ProcessShardAsync(
        string shardId,
        CancellationToken ct = default)
    {
        // ── Load shard ─────────────────────────────────────────────────────────
        var shard = await _db.RecurringBillingBatchShards
            .Include(s => s.Batch)
            .FirstOrDefaultAsync(s => s.Id == shardId && !s.IsDeleted, ct);

        if (shard is null)
            return Result<RecurringChargeWorkerResult>.Failure(
                "SHARD_NOT_FOUND", $"RecurringBillingBatchShard '{shardId}' not found.");

        if (shard.Batch is null)
            return Result<RecurringChargeWorkerResult>.Failure(
                "BATCH_NOT_FOUND", $"Parent batch for shard '{shardId}' not found.");

        var batchId = shard.BatchId;
        var now     = _dateTime.Now;

        // ── Skip already-Done shards (idempotency for multiple enqueue) ────────
        if (shard.Status == RecurringBillingBatchStatus.Done)
        {
            _logger.LogInformation(
                "RecurringChargeWorker: shard {ShardId} is already Done — skipping.", shardId);
            return Result<RecurringChargeWorkerResult>.Success(new RecurringChargeWorkerResult
            {
                ShardId   = shardId,
                Processed = shard.CasesProcessed,
                Skipped   = shard.CasesProcessed
            });
        }

        // ── Mark shard InProgress ─────────────────────────────────────────────
        shard.Status      = RecurringBillingBatchStatus.InProgress;
        shard.StartedAt   = now;
        shard.LastUpdateDate = now;
        shard.LastUpdateBy   = Actor;
        await _db.SaveChangesAsync(ct);

        // ── Mark parent batch InProgress (best-effort — another worker may beat us) ──
        if (shard.Batch.Status == RecurringBillingBatchStatus.Planned)
        {
            shard.Batch.Status        = RecurringBillingBatchStatus.InProgress;
            shard.Batch.StartedAt     = now;
            shard.Batch.LastUpdateDate = now;
            shard.Batch.LastUpdateBy   = Actor;
            await _db.SaveChangesAsync(ct);
        }

        // ── Determine already-processed state IDs for resumability ────────────
        // A state is "already processed in this batch" if it has a RecurringBillingAttempt
        // whose CreationDate is today. The processor's own NextAttemptDate guard prevents
        // double-charging regardless, but we skip for efficiency.
        var today = now.Date;
        var todayStart    = today;
        var tomorrowStart = today.AddDays(1);
        var processedToday = await _db.RecurringBillingAttempts
            .AsNoTracking()
            .Where(a => a.CreationDate >= todayStart
                     && a.CreationDate < tomorrowStart
                     && _db.SubscriptionBillingStates
                            .Any(s => s.Id == a.SubscriptionBillingStateId
                                   && s.ShardKey >= shard.IdRangeStart
                                   && s.ShardKey <= shard.IdRangeEnd))
            .Select(a => a.SubscriptionBillingStateId)
            .Distinct()
            .ToListAsync(ct);

        var processedSet = processedToday.ToHashSet();

        // ── Load due states in this shard range ───────────────────────────────
        var dueStateIds = await _db.SubscriptionBillingStates
            .AsNoTracking()
            .Where(s => s.ShardKey >= shard.IdRangeStart
                     && s.ShardKey <= shard.IdRangeEnd
                     && (s.Status == RecurringBillingStatus.Active
                      || s.Status == RecurringBillingStatus.Retrying
                      || s.Status == RecurringBillingStatus.AwaitingAnniversaryRetry)
                     && s.NextAttemptDate.Date <= today)
            .OrderBy(s => s.ShardKey)
            .Select(s => new { s.Id, s.MemberId, s.MembershipSubscriptionId })
            .ToListAsync(ct);

        _logger.LogInformation(
            "RecurringChargeWorker: shard {ShardId} — {Total} due states, {AlreadyProcessed} already done today.",
            shardId, dueStateIds.Count, processedSet.Count);

        int processed = 0, succeeded = 0, failed = 0, skipped = 0;

        foreach (var stateRef in dueStateIds)
        {
            if (processedSet.Contains(stateRef.Id))
            {
                skipped++;
                continue;
            }

            try
            {
                var result = await _processor.ProcessAsync(stateRef.Id, forceBillNow: false, ct);

                if (!result.IsSuccess)
                {
                    failed++;
                    _logger.LogWarning(
                        "RecurringChargeWorker: state {StateId} — processor error [{Code}]: {Error}",
                        stateRef.Id, result.ErrorCode, result.Error);
                    processed++;
                    continue;
                }

                processed++;
                var outcome = result.Value!.Outcome;

                switch (outcome)
                {
                    case "Success":
                        succeeded++;
                        await EmitActivatedEventsAsync(
                            batchId, stateRef.Id, stateRef.MemberId,
                            stateRef.MembershipSubscriptionId, result.Value.OrderId,
                            now, ct);
                        break;

                    case "Failed":
                        failed++;
                        await EmitDeactivatedEventIfNeededAsync(
                            batchId, stateRef.Id, stateRef.MemberId,
                            stateRef.MembershipSubscriptionId, now, ct);
                        break;

                    case "Skipped":
                    case "Scheduled":
                        skipped++;
                        break;

                    default:
                        failed++;
                        break;
                }
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex,
                    "RecurringChargeWorker: unhandled exception processing state {StateId}.",
                    stateRef.Id);
                processed++;
            }
        }

        // ── Mark shard Done ───────────────────────────────────────────────────
        shard.Status         = RecurringBillingBatchStatus.Done;
        shard.CompletedAt    = _dateTime.Now;
        shard.CasesProcessed = processed;
        shard.LastUpdateDate = _dateTime.Now;
        shard.LastUpdateBy   = Actor;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "RecurringChargeWorker: shard {ShardId} complete — Processed={P}, Success={S}, Failed={F}, Skipped={SK}.",
            shardId, processed, succeeded, failed, skipped);

        return Result<RecurringChargeWorkerResult>.Success(new RecurringChargeWorkerResult
        {
            ShardId   = shardId,
            Processed = processed,
            Succeeded = succeeded,
            Failed    = failed,
            Skipped   = skipped
        });
    }

    // ── Event emission ─────────────────────────────────────────────────────────

    /// <summary>
    /// Emits a positive PointDeltaEvent and CommissionTriggerQueue rows
    /// on a successful (Activated) renewal.
    /// </summary>
    private async Task EmitActivatedEventsAsync(
        string batchId,
        string billingStateId,
        string memberId,
        string subscriptionId,
        string? orderId,
        DateTime now,
        CancellationToken ct)
    {
        // Read contribution snapshot from the subscription (set on this renewal by the processor)
        var subscription = await _db.MembershipSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && !s.IsDeleted, ct);

        if (subscription is null) return;

        // If the processor result did not carry an orderId, nothing to emit
        if (string.IsNullOrEmpty(orderId)) return;

        // ── PointDeltaEvent (positive) ─────────────────────────────────────
        var deltaEvent = new PointDeltaEvent
        {
            BatchId         = batchId,
            OrderId         = orderId,
            MemberId        = memberId,
            EventType       = PointDeltaEventType.Activated,
            DualTeamDelta   = subscription.DualTeamContribution,
            EnrollmentDelta = subscription.EnrollmentContribution,
            PersonalDelta   = subscription.PersonalContribution,
            OccurredAt      = now,
            Status          = PointDeltaEventStatus.Queued,
            CreatedBy       = Actor,
            CreationDate    = now
        };
        _db.PointDeltaEvents.Add(deltaEvent);

        // ── CommissionTriggerQueue rows ────────────────────────────────────
        foreach (var triggerType in CommissionTriggerTypes)
        {
            _db.CommissionTriggerQueues.Add(new CommissionTriggerQueue
            {
                BatchId      = batchId,
                MemberId     = memberId,
                OrderId      = orderId,
                TriggerType  = triggerType,
                IsProcessed  = false,
                CreatedBy    = Actor,
                CreationDate = now
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Emits a negative PointDeltaEvent when a billing failure transitions
    /// a subscription to Stopped (Deactivated). Only emitted if the subscription
    /// was previously contributing points (i.e. contribution > 0).
    /// </summary>
    private async Task EmitDeactivatedEventIfNeededAsync(
        string batchId,
        string billingStateId,
        string memberId,
        string subscriptionId,
        DateTime now,
        CancellationToken ct)
    {
        // Only emit if state transitioned to Stopped as part of this failure
        var billingState = await _db.SubscriptionBillingStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == billingStateId, ct);

        if (billingState?.Status != RecurringBillingStatus.Stopped) return;

        var subscription = await _db.MembershipSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && !s.IsDeleted, ct);

        if (subscription is null) return;

        // No contribution to remove — skip to avoid noise
        if (subscription.DualTeamContribution == 0
         && subscription.EnrollmentContribution == 0
         && subscription.PersonalContribution == 0)
            return;

        var failStart = now.Date;
        var failEnd   = failStart.AddDays(1);
        var failAttemptOrderId = await _db.RecurringBillingAttempts
            .AsNoTracking()
            .Where(a => a.SubscriptionBillingStateId == billingStateId
                     && a.CreationDate >= failStart
                     && a.CreationDate < failEnd)
            .Select(a => a.OrderId)
            .FirstOrDefaultAsync(ct);

        var deactivateEvent = new PointDeltaEvent
        {
            BatchId         = batchId,
            OrderId         = failAttemptOrderId ?? string.Empty,
            MemberId        = memberId,
            EventType       = PointDeltaEventType.Deactivated,
            DualTeamDelta   = -subscription.DualTeamContribution,
            EnrollmentDelta = -subscription.EnrollmentContribution,
            PersonalDelta   = -subscription.PersonalContribution,
            OccurredAt      = now,
            Status          = PointDeltaEventStatus.Queued,
            CreatedBy       = Actor,
            CreationDate    = now
        };
        _db.PointDeltaEvents.Add(deactivateEvent);
        await _db.SaveChangesAsync(ct);
    }
}
