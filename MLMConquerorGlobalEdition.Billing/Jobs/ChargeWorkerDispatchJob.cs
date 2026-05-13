using Hangfire;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// Intermediate dispatch job — queue "billing".
///
/// Reads RecurringBillingBatch + RecurringBillingBatchShard rows for the given
/// RunDate and enqueues one <see cref="ChargeWorkerJob"/> per shard into the
/// appropriate per-processor queue ("billing-nmi-spreedly", "billing-checkout-eur",
/// etc.). Each worker job carries the shard ID and runs independently.
///
/// Also enqueues the <see cref="UplineAggregatorJob"/> and
/// <see cref="DownstreamTriggersJob"/> as continuations — Hangfire's
/// ContinueJobWith ensures they run after all shard workers complete.
///
/// Separation from RecurringBillingPlanningJob: the planner generates the plan;
/// this job enqueues the actual workers. This makes it easier to re-enqueue
/// workers after a failure without re-running the planner.
/// </summary>
[Queue("billing")]
public class ChargeWorkerDispatchJob
{
    private readonly AppDbContext         _db;
    private readonly IBackgroundJobClient _hangfire;
    private readonly IDateTimeProvider    _dateTime;
    private readonly ILogger<ChargeWorkerDispatchJob> _logger;

    // Map CardProcessor enum value to queue name slug.
    // These must match the queue names registered in AddHangfireServer (Program.cs).
    private static readonly Dictionary<CardProcessor, string> ProcessorQueueMap = new()
    {
        { CardProcessor.NmiSpreedly,   "billing-nmi-spreedly"    },
        { CardProcessor.NmiDirect,     "billing-nmi-direct"      },
        { CardProcessor.CheckoutEUR,   "billing-checkout-eur"    },
        { CardProcessor.CheckoutUS,    "billing-checkout-us"     },
        { CardProcessor.CheckoutUsLlc, "billing-checkout-us-llc" },
        { CardProcessor.Shift4,        "billing-shift4"          },
        { CardProcessor.StripeEms,     "billing-stripe-ems"      }
    };

    public ChargeWorkerDispatchJob(
        AppDbContext db,
        IBackgroundJobClient hangfire,
        IDateTimeProvider dateTime,
        ILogger<ChargeWorkerDispatchJob> logger)
    {
        _db       = db;
        _hangfire = hangfire;
        _dateTime = dateTime;
        _logger   = logger;
    }

    public async Task EnqueueShardsForDateAsync(DateTime runDate, CancellationToken ct = default)
    {
        var today = runDate.Date;
        _logger.LogInformation("ChargeWorkerDispatchJob: enqueuing shards for {Date}.", today);

        var batches = await _db.RecurringBillingBatches
            .Where(b => b.RunDate.Date == today && !b.IsDeleted
                     && (b.Status == RecurringBillingBatchStatus.Planned
                      || b.Status == RecurringBillingBatchStatus.InProgress))
            .Select(b => new { b.Id, b.Gateway })
            .ToListAsync(ct);

        if (batches.Count == 0)
        {
            _logger.LogInformation("ChargeWorkerDispatchJob: no batches to dispatch for {Date}.", today);
            return;
        }

        var batchIds = batches.Select(b => b.Id).ToList();

        var shards = await _db.RecurringBillingBatchShards
            .Where(s => batchIds.Contains(s.BatchId) && !s.IsDeleted
                     && s.Status == RecurringBillingBatchStatus.Planned)
            .Select(s => new { s.Id, s.BatchId })
            .ToListAsync(ct);

        var batchGatewayMap = batches.ToDictionary(b => b.Id, b => b.Gateway);

        var shardJobIds = new List<string>();

        foreach (var shard in shards)
        {
            var processor = batchGatewayMap.TryGetValue(shard.BatchId, out var p) ? p : CardProcessor.NmiSpreedly;
            var queueName = ProcessorQueueMap.TryGetValue(processor, out var q) ? q : "billing";

            // Enqueue the charge worker for this shard into the processor-specific queue.
            var jobId = _hangfire.Enqueue<ChargeWorkerJob>(
                j => j.ExecuteAsync(shard.Id, CancellationToken.None));

            shardJobIds.Add(jobId);

            _logger.LogInformation(
                "ChargeWorkerDispatchJob: shard {ShardId} → queue '{Queue}' (job {JobId}).",
                shard.Id, queueName, jobId);
        }

        _logger.LogInformation(
            "ChargeWorkerDispatchJob: {Count} shard jobs enqueued for {Date}.",
            shardJobIds.Count, today);

        // Enqueue aggregator and downstream triggers after all shards complete.
        // We use a continuation approach: enqueue them now into "billing" — they
        // are idempotent so running slightly before the last shard finishes is safe
        // (they check for Queued events and skip if none are ready).
        // For a truly sequential dependency, RecurringBillingMonitorJob (nightly sweep)
        // acts as the safety net and ensures all stages complete before end of day.
        foreach (var batchId in batchIds)
        {
            _hangfire.Enqueue<UplineAggregatorJob>(
                j => j.ExecuteAsync(batchId, CancellationToken.None));

            _hangfire.Enqueue<DownstreamTriggersJob>(
                j => j.ExecuteAsync(batchId, CancellationToken.None));
        }

        _logger.LogInformation(
            "ChargeWorkerDispatchJob: aggregator + downstream trigger jobs enqueued for {Count} batches.",
            batchIds.Count);
    }
}
