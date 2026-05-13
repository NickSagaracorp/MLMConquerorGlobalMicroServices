using Hangfire;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// HangFire recurring job — daily at <c>RecurringBilling:BatchStartTimeUtc</c>
/// (default 05:00 UTC), queue "billing".
///
/// Responsibilities:
/// Stage 1 of the high-volume pipeline (§10.3):
///   1. Queries SubscriptionBillingState rows due today.
///   2. Routes them in dry mode to determine per-processor worker counts.
///   3. Writes RecurringBillingBatch + RecurringBillingBatchShard rows.
///   4. Enqueues one ChargeWorkerJob per shard into the appropriate
///      per-processor Hangfire queue ("billing-{processor}").
///
/// Idempotent: RecurringBillingPlanner skips the plan if it already exists
/// for today's RunDate. Job enqueue is conditional — shards are only enqueued
/// when BatchesCreated > 0.
/// </summary>
[Queue("billing")]
public class RecurringBillingPlanningJob
{
    private readonly IRecurringBillingPlanner      _planner;
    private readonly IBackgroundJobClient          _hangfire;
    private readonly IDateTimeProvider             _dateTime;
    private readonly ILogger<RecurringBillingPlanningJob> _logger;

    public RecurringBillingPlanningJob(
        IRecurringBillingPlanner planner,
        IBackgroundJobClient hangfire,
        IDateTimeProvider dateTime,
        ILogger<RecurringBillingPlanningJob> logger)
    {
        _planner  = planner;
        _hangfire = hangfire;
        _dateTime = dateTime;
        _logger   = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var today = _dateTime.Now.Date;
        _logger.LogInformation("RecurringBillingPlanningJob: starting for {Date}.", today);

        var result = await _planner.PlanAsync(today, ct);
        if (!result.IsSuccess)
        {
            _logger.LogError(
                "RecurringBillingPlanningJob: planner failed [{Code}]: {Error}",
                result.ErrorCode, result.Error);
            return;
        }

        var plan = result.Value!;
        _logger.LogInformation(
            "RecurringBillingPlanningJob: plan complete — {Batches} batches, {Cases} cases, {Shards} shards.",
            plan.BatchesCreated, plan.TotalCases, plan.TotalShards);

        if (plan.BatchesCreated == 0)
        {
            _logger.LogInformation(
                "RecurringBillingPlanningJob: no new batches (either no due states or already planned). Done.");
            return;
        }

        // ── Enqueue one ChargeWorkerJob per shard ──────────────────────────
        // The shard jobs are enqueued into per-processor queues determined
        // by the batch's Gateway field ("billing-{processor-slug}").
        // We load the newly created shards to get their IDs and parent batch metadata.
        _hangfire.Enqueue<ChargeWorkerDispatchJob>(
            j => j.EnqueueShardsForDateAsync(today, CancellationToken.None));

        _logger.LogInformation(
            "RecurringBillingPlanningJob: shard dispatch job enqueued for {Date}.", today);
    }
}
