using Hangfire;
using MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// HangFire fire-and-forget job — queue "billing".
///
/// Stage 4 of the high-volume pipeline (§10.6).
/// Invoked by <see cref="ChargeWorkerDispatchJob"/> after shard workers are enqueued.
/// Delegates entirely to <see cref="IDownstreamTriggers.DispatchAsync"/>.
///
/// Dispatches:
///   - RankEvaluationQueue entries for affected upline members.
///   - CommissionTriggerQueue entries → "commissions" Hangfire queue.
///   - Push notifications to successfully renewed members.
///
/// Idempotent: CommissionTriggerQueue.IsProcessed = true after dispatch;
/// push notifications are fire-and-forget.
/// </summary>
[Queue("billing")]
public class DownstreamTriggersJob
{
    private readonly IDownstreamTriggers               _triggers;
    private readonly ILogger<DownstreamTriggersJob>    _logger;

    public DownstreamTriggersJob(
        IDownstreamTriggers triggers,
        ILogger<DownstreamTriggersJob> logger)
    {
        _triggers = triggers;
        _logger   = logger;
    }

    public async Task ExecuteAsync(string batchId, CancellationToken ct = default)
    {
        _logger.LogInformation("DownstreamTriggersJob: starting dispatch for batch {BatchId}.", batchId);

        var result = await _triggers.DispatchAsync(batchId, ct);

        if (!result.IsSuccess)
        {
            _logger.LogError(
                "DownstreamTriggersJob: batch {BatchId} failed [{Code}]: {Error}",
                batchId, result.ErrorCode, result.Error);

            throw new InvalidOperationException(
                $"DownstreamTriggers failed for batch '{batchId}': [{result.ErrorCode}] {result.Error}");
        }

        var r = result.Value!;
        _logger.LogInformation(
            "DownstreamTriggersJob: batch {BatchId} complete — Rank={R}, Commissions={C}, Push={P}.",
            batchId, r.RankQueueEntriesAdded, r.CommissionTriggersProcessed, r.PushNotificationsEnqueued);
    }
}
