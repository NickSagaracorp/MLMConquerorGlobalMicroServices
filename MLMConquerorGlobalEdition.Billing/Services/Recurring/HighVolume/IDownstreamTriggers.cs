using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;

public class DownstreamTriggersResult
{
    public int RankQueueEntriesAdded          { get; init; }
    public int CommissionTriggersProcessed    { get; init; }
    public int PushNotificationsEnqueued      { get; init; }
}

/// <summary>
/// Stage 4 of the high-volume pipeline (BILLING-RULES §10.6).
///
/// After the upline aggregator (Stage 3) has applied all point deltas, this
/// service propagates the downstream effects of the billing run:
///
/// 1. Rank re-evaluation: for every upline member whose stats were updated,
///    inserts a <see cref="RankEvaluationQueue"/> entry so the RankEngine
///    recalculates their rank in its next processing window.
///
/// 2. Commission triggers: processes unprocessed <see cref="CommissionTriggerQueue"/>
///    rows for the batch — enqueues one Hangfire job per trigger (FastStartBonus /
///    BoostBonus) into the "commissions" queue.
///
/// 3. Push notifications: enqueues renewal-success push notifications for each
///    member who was successfully charged in this batch (Activated events).
///
/// Idempotent: CommissionTriggerQueue.IsProcessed = true after dispatch;
/// RankEvaluationQueue rows are inserted without unique constraint (the rank
/// processor deduplicates by evaluateMemberId + triggerDate).
/// </summary>
public interface IDownstreamTriggers
{
    Task<Result<DownstreamTriggersResult>> DispatchAsync(
        string batchId,
        CancellationToken ct = default);
}
