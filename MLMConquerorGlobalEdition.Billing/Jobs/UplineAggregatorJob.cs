using Hangfire;
using MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// HangFire fire-and-forget job — queue "billing".
///
/// Stage 3 of the high-volume pipeline (§10.5).
/// Invoked by <see cref="ChargeWorkerDispatchJob"/> after shard workers are enqueued.
/// Delegates entirely to <see cref="IUplineAggregator.AggregateAsync"/>.
///
/// Idempotent: the aggregator processes only Queued PointDeltaEvents and marks
/// them Applied. A re-enqueue after partial completion resumes from the
/// remaining Queued rows.
/// </summary>
[Queue("billing")]
public class UplineAggregatorJob
{
    private readonly IUplineAggregator                _aggregator;
    private readonly ILogger<UplineAggregatorJob>     _logger;

    public UplineAggregatorJob(
        IUplineAggregator aggregator,
        ILogger<UplineAggregatorJob> logger)
    {
        _aggregator = aggregator;
        _logger     = logger;
    }

    public async Task ExecuteAsync(string batchId, CancellationToken ct = default)
    {
        _logger.LogInformation("UplineAggregatorJob: starting aggregation for batch {BatchId}.", batchId);

        var result = await _aggregator.AggregateAsync(batchId, ct);

        if (!result.IsSuccess)
        {
            _logger.LogError(
                "UplineAggregatorJob: batch {BatchId} failed [{Code}]: {Error}",
                batchId, result.ErrorCode, result.Error);

            throw new InvalidOperationException(
                $"UplineAggregator failed for batch '{batchId}': [{result.ErrorCode}] {result.Error}");
        }

        var r = result.Value!;
        _logger.LogInformation(
            "UplineAggregatorJob: batch {BatchId} complete — {Events} events applied, {Members} upline members updated.",
            batchId, r.EventsApplied, r.UplineMembersUpdated);
    }
}
