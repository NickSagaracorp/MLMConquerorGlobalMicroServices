using Hangfire;
using MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// HangFire fire-and-forget job — per-processor queues
/// ("billing-nmi-spreedly", "billing-checkout-eur", etc.).
///
/// Processes a single RecurringBillingBatchShard end-to-end by delegating to
/// <see cref="IRecurringChargeWorker"/>. This is the leaf unit of work in the
/// high-volume pipeline — one job per shard, N jobs per batch, potentially
/// hundreds of jobs per day across all processors.
///
/// The queue attribute here is overridden at dispatch time by
/// <see cref="ChargeWorkerDispatchJob"/>, which enqueues to the processor-specific
/// queue. The attribute below is the fallback if the job is scheduled directly.
/// </summary>
[Queue("billing")]
public class ChargeWorkerJob
{
    private readonly IRecurringChargeWorker           _worker;
    private readonly ILogger<ChargeWorkerJob>         _logger;

    public ChargeWorkerJob(
        IRecurringChargeWorker worker,
        ILogger<ChargeWorkerJob> logger)
    {
        _worker = worker;
        _logger = logger;
    }

    public async Task ExecuteAsync(string shardId, CancellationToken ct = default)
    {
        _logger.LogInformation("ChargeWorkerJob: processing shard {ShardId}.", shardId);

        var result = await _worker.ProcessShardAsync(shardId, ct);

        if (!result.IsSuccess)
        {
            _logger.LogError(
                "ChargeWorkerJob: shard {ShardId} failed [{Code}]: {Error}",
                shardId, result.ErrorCode, result.Error);

            // Re-throw so Hangfire marks the job as Failed and applies retry policy.
            throw new InvalidOperationException(
                $"RecurringChargeWorker failed for shard '{shardId}': [{result.ErrorCode}] {result.Error}");
        }

        var r = result.Value!;
        _logger.LogInformation(
            "ChargeWorkerJob: shard {ShardId} complete — Processed={P}, Success={S}, Failed={F}, Skipped={SK}.",
            shardId, r.Processed, r.Succeeded, r.Failed, r.Skipped);
    }
}
