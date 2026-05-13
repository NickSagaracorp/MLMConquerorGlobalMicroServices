using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Routing;

/// <summary>
/// Deterministic persisted-counter split selector.
///
/// Algorithm:
///   1. Load all counter rows for the bucket.
///   2. Compute total attempts = sum of all counters.
///   3. For each processor: target = (weight% / 100) * (totalAttempts + 1).
///      deficit = target - actual.
///   4. Choose the processor with the largest deficit.
///      Stable tie-break: lower SortOrder wins.
///   5. Increment the chosen processor's counter (same DB transaction as the charge).
///
/// Long-run behaviour: exact percentages regardless of concurrency because
/// the counter is incremented transactionally.
/// </summary>
public class GatewaySplitSelector : IGatewaySplitSelector
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;

    public GatewaySplitSelector(AppDbContext db, IDateTimeProvider dateTime)
    {
        _db       = db;
        _dateTime = dateTime;
    }

    public async Task<Result<CardProcessor>> PickAsync(
        string routeBucketKey,
        IReadOnlyList<GatewayRoutingRuleSplit> splits,
        CancellationToken ct = default)
    {
        if (splits.Count == 0)
            return Result<CardProcessor>.Failure("NO_SPLITS", "Routing rule has no splits.");

        if (splits.Count == 1)
        {
            await IncrementCounterAsync(routeBucketKey, splits[0].CardProcessor, ct);
            return Result<CardProcessor>.Success(splits[0].CardProcessor);
        }

        // Load existing counters for this bucket
        var counters = await _db.GatewayRoutingCounters
            .Where(c => c.RouteBucketKey == routeBucketKey)
            .ToListAsync(ct);

        var totalAttempts = counters.Sum(c => c.AttemptCount);
        long nextTotal    = totalAttempts + 1;

        CardProcessor? best        = null;
        double         bestDeficit = double.MinValue;
        int            bestOrder   = int.MaxValue;

        foreach (var split in splits.OrderBy(s => s.SortOrder))
        {
            var actual = counters.FirstOrDefault(c => c.CardProcessor == split.CardProcessor)?.AttemptCount ?? 0L;
            var target  = (double)split.WeightPercent / 100.0 * nextTotal;
            var deficit = target - actual;

            if (deficit > bestDeficit || (deficit == bestDeficit && split.SortOrder < bestOrder))
            {
                bestDeficit = deficit;
                best        = split.CardProcessor;
                bestOrder   = split.SortOrder;
            }
        }

        var chosen = best!.Value;
        await IncrementCounterAsync(routeBucketKey, chosen, ct);
        return Result<CardProcessor>.Success(chosen);
    }

    private async Task IncrementCounterAsync(
        string routeBucketKey, CardProcessor processor, CancellationToken ct)
    {
        var counter = await _db.GatewayRoutingCounters
            .FirstOrDefaultAsync(c => c.RouteBucketKey == routeBucketKey
                                   && c.CardProcessor  == processor, ct);

        if (counter is null)
        {
            counter = new GatewayRoutingCounter
            {
                RouteBucketKey = routeBucketKey,
                CardProcessor  = processor,
                AttemptCount   = 1,
                CreatedBy      = "billing-engine",
                CreationDate   = _dateTime.Now,
            };
            _db.GatewayRoutingCounters.Add(counter);
        }
        else
        {
            counter.AttemptCount++;
        }
    }
}
