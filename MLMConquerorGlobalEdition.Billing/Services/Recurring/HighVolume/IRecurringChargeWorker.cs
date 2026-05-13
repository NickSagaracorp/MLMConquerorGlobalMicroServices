using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;

public class RecurringChargeWorkerResult
{
    public string ShardId         { get; init; } = string.Empty;
    public int    Processed       { get; init; }
    public int    Succeeded       { get; init; }
    public int    Failed          { get; init; }
    public int    Skipped         { get; init; }
}

/// <summary>
/// Stage 2 of the high-volume pipeline (BILLING-RULES §10.4).
///
/// Processes one <see cref="RecurringBillingBatchShard"/> by iterating
/// SubscriptionBillingState rows whose ShardKey falls within [shard.IdRangeStart,
/// shard.IdRangeEnd]. Delegates each individual charge to the existing
/// <see cref="IRecurringBillingProcessor"/> (commission-balance → card path).
///
/// After each successful charge (Outcome == "Success"), emits one
/// <see cref="PointDeltaEvent"/> row and one <see cref="CommissionTriggerQueue"/>
/// row per applicable commission type. After each failure that produces an
/// Activated → Deactivated transition, emits a negative-delta PointDeltaEvent.
///
/// Resumable: if the worker crashes mid-shard, the next worker attachment
/// re-reads the shard, queries already-processed state IDs from
/// RecurringBillingAttempt, and skips them.
///
/// Idempotent per state: the processor's own NextAttemptDate guard prevents
/// double-charging. The worker additionally skips states already present in
/// RecurringBillingAttempt for today's batch.
/// </summary>
public interface IRecurringChargeWorker
{
    Task<Result<RecurringChargeWorkerResult>> ProcessShardAsync(
        string shardId,
        CancellationToken ct = default);
}
