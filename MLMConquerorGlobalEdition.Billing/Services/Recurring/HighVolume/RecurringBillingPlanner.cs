using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;

/// <summary>
/// Stage 1 of the high-volume pipeline (BILLING-RULES §10.3).
///
/// Algorithm:
/// 1. Query SubscriptionBillingStates due today (Status Active/Retrying/AwaitingAnniversaryRetry,
///    NextAttemptDate <= today).
/// 2. For each, run IGatewayRouter in dry mode (admin-override = null; we use the member's
///    stored card country from their MemberProfile to determine routing).
///    Since full routing requires card brand + country and we're in dry mode, we approximate
///    with the member's profile country + Visa as the brand (conservative — most members pay
///    with Visa/MC). This is only used for worker-count planning; the actual charge uses the
///    real card brand.
/// 3. Group cases by resolved CardProcessor.
/// 4. Read average latency from GatewayChargeAttempt over LatencySamplingDays.
/// 5. Compute WorkersNeeded = ceil(cases × avgLatencyMs / (windowMs × 1)) — concurrencyFactor=1
///    because each Hangfire worker handles one state at a time.
/// 6. Apply floor (MinWorkersPerGateway:P) and ceiling (MaxConcurrencyPerGateway:P).
/// 7. Write RecurringBillingBatch + RecurringBillingBatchShard rows.
/// 8. Return the plan summary.
///
/// Idempotent: if batches already exist for this RunDate, returns their summary without
/// creating new rows.
/// </summary>
public class RecurringBillingPlanner : IRecurringBillingPlanner
{
    private readonly AppDbContext              _db;
    private readonly IGatewayRouter            _router;
    private readonly IDateTimeProvider         _dateTime;
    private readonly ILogger<RecurringBillingPlanner> _logger;

    // Default parameter values (overridden by GlobalParameter rows when seeded)
    private const int DefaultTargetWindowHours  = 3;
    private const int DefaultLatencySamplingDays = 14;
    private const int DefaultMinWorkers         = 2;
    private const int DefaultMaxWorkers         = 10;
    private const double DefaultAvgLatencyMs    = 800.0; // conservative estimate for new installs

    public RecurringBillingPlanner(
        AppDbContext db,
        IGatewayRouter router,
        IDateTimeProvider dateTime,
        ILogger<RecurringBillingPlanner> logger)
    {
        _db       = db;
        _router   = router;
        _dateTime = dateTime;
        _logger   = logger;
    }

    public async Task<Result<RecurringBillingPlannerResult>> PlanAsync(
        DateTime runDate,
        CancellationToken ct = default)
    {
        var today = runDate.Date;

        // ── Idempotency check ─────────────────────────────────────────────────
        var existingBatches = await _db.RecurringBillingBatches
            .Where(b => b.RunDate.Date == today && !b.IsDeleted)
            .ToListAsync(ct);

        if (existingBatches.Count > 0)
        {
            var existingShardsCount = await _db.RecurringBillingBatchShards
                .Where(s => existingBatches.Select(b => b.Id).Contains(s.BatchId) && !s.IsDeleted)
                .CountAsync(ct);

            _logger.LogInformation(
                "RecurringBillingPlanner: plan for {Date} already exists ({Batches} batches, {Shards} shards). Skipping.",
                today, existingBatches.Count, existingShardsCount);

            return Result<RecurringBillingPlannerResult>.Success(new RecurringBillingPlannerResult
            {
                BatchesCreated = 0,
                TotalCases     = existingBatches.Sum(b => b.CaseCount),
                TotalShards    = existingShardsCount
            });
        }

        // ── Load tunables from GlobalParameter ────────────────────────────────
        var parameters = await _db.GlobalParameters
            .AsNoTracking()
            .ToListAsync(ct);

        int targetWindowHours  = GetInt(parameters, "RecurringBilling:TargetCompletionWindowHours", DefaultTargetWindowHours);
        int latencySamplingDays = GetInt(parameters, "RecurringBilling:LatencySamplingDays", DefaultLatencySamplingDays);
        var windowMs           = (double)targetWindowHours * 3_600_000;

        // ── Scheduled start time from GlobalParameter ─────────────────────────
        var batchStartTimeStr = GetString(parameters, "RecurringBilling:BatchStartTimeUtc", "05:00");
        DateTime scheduledStart;
        if (TimeSpan.TryParse(batchStartTimeStr, out var batchTs))
            scheduledStart = today.Add(batchTs);
        else
            scheduledStart = today.AddHours(5);

        // ── Find due states ────────────────────────────────────────────────────
        var dueStates = await _db.SubscriptionBillingStates
            .AsNoTracking()
            .Where(s => (s.Status == RecurringBillingStatus.Active
                      || s.Status == RecurringBillingStatus.Retrying
                      || s.Status == RecurringBillingStatus.AwaitingAnniversaryRetry)
                     && s.NextAttemptDate.Date <= today)
            .OrderBy(s => s.ShardKey)
            .Select(s => new { s.ShardKey, s.MemberId, s.RecurringBillingPlanId })
            .ToListAsync(ct);

        if (dueStates.Count == 0)
        {
            _logger.LogInformation("RecurringBillingPlanner: no due states for {Date}. No batches created.", today);
            return Result<RecurringBillingPlannerResult>.Success(new RecurringBillingPlannerResult
            {
                BatchesCreated = 0,
                TotalCases     = 0,
                TotalShards    = 0
            });
        }

        _logger.LogInformation(
            "RecurringBillingPlanner: {Count} due states found for {Date}. Computing routing in dry mode…",
            dueStates.Count, today);

        // ── Dry-mode routing: group states by processor ───────────────────────
        // For planning purposes, we use the member's country + Visa brand (most common).
        // The actual charge uses the real card brand from MemberCreditCard.
        var memberCountries = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => dueStates.Select(s => s.MemberId).Contains(m.MemberId))
            .Select(m => new { m.MemberId, m.Country })
            .ToDictionaryAsync(m => m.MemberId, m => string.IsNullOrEmpty(m.Country) ? "US" : m.Country, ct);

        var casesByProcessor = new Dictionary<CardProcessor, List<long>>();

        foreach (var state in dueStates)
        {
            var country = memberCountries.TryGetValue(state.MemberId, out var c) ? c : "US";
            var dryCtx  = new GatewayRoutingContext
            {
                OperationType        = BillingOperationType.Payment,
                CardBrand            = CardBrand.Visa,  // dry-mode approximation
                CardholderCountryIso = country,
                AmountUsd            = 0m,              // amount irrelevant for planning
                AdminOverride        = null
            };

            var routeResult = await _router.ResolveAsync(dryCtx, ct);
            var processor   = routeResult.IsSuccess
                ? routeResult.Value!.Steps[0].CardProcessor
                : CardProcessor.NmiSpreedly;  // fallback to default if routing fails

            if (!casesByProcessor.TryGetValue(processor, out var list))
            {
                list = new List<long>();
                casesByProcessor[processor] = list;
            }
            list.Add(state.ShardKey);
        }

        // ── Read average latency per processor ────────────────────────────────
        var latencyCutoff = _dateTime.Now.AddDays(-latencySamplingDays);
        var avgLatencies  = await _db.GatewayChargeAttempts
            .AsNoTracking()
            .Where(a => a.CreationDate >= latencyCutoff && a.Outcome != "Scheduled")
            .GroupBy(a => a.CardProcessor)
            .Select(g => new
            {
                Processor    = g.Key,
                AvgLatencyMs = g.Average(a => a.LatencyMs ?? (long)DefaultAvgLatencyMs)
            })
            .ToDictionaryAsync(x => x.Processor, x => x.AvgLatencyMs, ct);

        // ── Create batches + shards ───────────────────────────────────────────
        var now           = _dateTime.Now;
        var createdBy     = "recurring-billing-planner";
        int batchesCreated = 0;
        int totalShards   = 0;

        foreach (var (processor, shardKeys) in casesByProcessor)
        {
            var avgLatencyMs = avgLatencies.TryGetValue(processor, out var lat) ? lat : DefaultAvgLatencyMs;
            var minWorkers   = GetInt(parameters, $"RecurringBilling:MinWorkersPerGateway:{processor}", DefaultMinWorkers);
            var maxWorkers   = GetInt(parameters, $"RecurringBilling:MaxConcurrencyPerGateway:{processor}", DefaultMaxWorkers);
            var windowOffset = GetInt(parameters, $"RecurringBilling:GatewayWindowOffsetMinutes:{processor}", 0);

            // WorkersNeeded = ceil(cases × avgLatencyMs / windowMs)
            var workersNeeded = (int)Math.Ceiling((double)shardKeys.Count * avgLatencyMs / windowMs);
            workersNeeded     = Math.Max(minWorkers, Math.Min(maxWorkers, workersNeeded));

            var gatewayScheduledStart = scheduledStart.AddMinutes(windowOffset);

            var batch = new RecurringBillingBatch
            {
                Id                 = Guid.NewGuid().ToString(),
                RunDate            = today,
                Gateway            = processor,
                WorkerCount        = workersNeeded,
                ScheduledStartTime = gatewayScheduledStart,
                CaseCount          = shardKeys.Count,
                Status             = RecurringBillingBatchStatus.Planned,
                CreatedBy          = createdBy,
                CreationDate       = now,
                LastUpdateDate     = now
            };

            // Partition shardKeys evenly across workers
            var shards = PartitionIntoShards(shardKeys, workersNeeded, createdBy, now, batch.Id);
            foreach (var shard in shards)
                batch.Shards.Add(shard);

            _db.RecurringBillingBatches.Add(batch);
            batchesCreated++;
            totalShards += shards.Count;

            _logger.LogInformation(
                "RecurringBillingPlanner: {Processor} — {Cases} cases → {Workers} workers, {Shards} shards, " +
                "avg latency {Latency:F0} ms, window {Window} h.",
                processor, shardKeys.Count, workersNeeded, shards.Count, avgLatencyMs, targetWindowHours);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "RecurringBillingPlanner: plan complete — {Batches} batches, {Cases} total cases, {Shards} shards.",
            batchesCreated, dueStates.Count, totalShards);

        return Result<RecurringBillingPlannerResult>.Success(new RecurringBillingPlannerResult
        {
            BatchesCreated = batchesCreated,
            TotalCases     = dueStates.Count,
            TotalShards    = totalShards
        });
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Approach: dedicated PreviewAsync method that re-uses the same query and
    /// computation logic as PlanAsync but skips the persist step entirely.
    /// No RecurringBillingBatch or RecurringBillingBatchShard rows are written.
    /// </remarks>
    public async Task<Result<RecurringBillingPlannerPreview>> PreviewAsync(
        DateTime runDate,
        CancellationToken ct = default)
    {
        var today = runDate.Date;
        var now   = _dateTime.Now;

        // ── Load tunables ──────────────────────────────────────────────────────
        var parameters = await _db.GlobalParameters
            .AsNoTracking()
            .ToListAsync(ct);

        int targetWindowHours   = GetInt(parameters, "RecurringBilling:TargetCompletionWindowHours", DefaultTargetWindowHours);
        int latencySamplingDays = GetInt(parameters, "RecurringBilling:LatencySamplingDays", DefaultLatencySamplingDays);
        var windowMs            = (double)targetWindowHours * 3_600_000;

        // ── Find due states (count + routing, same as PlanAsync) ───────────────
        var dueStates = await _db.SubscriptionBillingStates
            .AsNoTracking()
            .Where(s => (s.Status == RecurringBillingStatus.Active
                      || s.Status == RecurringBillingStatus.Retrying
                      || s.Status == RecurringBillingStatus.AwaitingAnniversaryRetry)
                     && s.NextAttemptDate.Date <= today)
            .OrderBy(s => s.ShardKey)
            .Select(s => new { s.ShardKey, s.MemberId })
            .ToListAsync(ct);

        if (dueStates.Count == 0)
        {
            return Result<RecurringBillingPlannerPreview>.Success(new RecurringBillingPlannerPreview
            {
                AsOfUtc           = now,
                PendingBills      = 0,
                TargetWindowHours = targetWindowHours,
                PerGateway        = new List<GatewayPreviewRow>(),
                EstimatedCompletionMinutes = 0,
                WithinTarget      = true,
                Notes             = new List<string>()
            });
        }

        // ── Dry-mode routing ───────────────────────────────────────────────────
        var memberCountries = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => dueStates.Select(s => s.MemberId).Contains(m.MemberId))
            .Select(m => new { m.MemberId, m.Country })
            .ToDictionaryAsync(m => m.MemberId, m => string.IsNullOrEmpty(m.Country) ? "US" : m.Country, ct);

        var casesByProcessor = new Dictionary<CardProcessor, int>();

        foreach (var state in dueStates)
        {
            var country = memberCountries.TryGetValue(state.MemberId, out var c) ? c : "US";
            var dryCtx  = new GatewayRoutingContext
            {
                OperationType        = BillingOperationType.Payment,
                CardBrand            = CardBrand.Visa,
                CardholderCountryIso = country,
                AmountUsd            = 0m,
                AdminOverride        = null
            };

            var routeResult = await _router.ResolveAsync(dryCtx, ct);
            var processor   = routeResult.IsSuccess
                ? routeResult.Value!.Steps[0].CardProcessor
                : CardProcessor.NmiSpreedly;

            casesByProcessor.TryGetValue(processor, out var existing);
            casesByProcessor[processor] = existing + 1;
        }

        // ── Read average latency ───────────────────────────────────────────────
        var latencyCutoff = now.AddDays(-latencySamplingDays);
        var avgLatencies  = await _db.GatewayChargeAttempts
            .AsNoTracking()
            .Where(a => a.CreationDate >= latencyCutoff && a.Outcome != "Scheduled")
            .GroupBy(a => a.CardProcessor)
            .Select(g => new
            {
                Processor    = g.Key,
                AvgLatencyMs = g.Average(a => a.LatencyMs ?? (long)DefaultAvgLatencyMs)
            })
            .ToDictionaryAsync(x => x.Processor, x => x.AvgLatencyMs, ct);

        // ── Compute per-gateway rows ───────────────────────────────────────────
        var notes    = new List<string>();
        var rows     = new List<GatewayPreviewRow>();
        double worstGatewayMinutes = 0;
        double maxOffset           = 0;

        foreach (var (processor, cases) in casesByProcessor)
        {
            double avgMs;

            if (avgLatencies.TryGetValue(processor, out var dbAvg))
            {
                avgMs = dbAvg;
            }
            else
            {
                avgMs = DefaultAvgLatencyMs;
                notes.Add($"{processor}: no historical latency data found; defaulted to {DefaultAvgLatencyMs:F0} ms.");
            }

            var minWorkers = GetInt(parameters, $"RecurringBilling:MinWorkersPerGateway:{processor}", DefaultMinWorkers);
            var maxWorkers = GetInt(parameters, $"RecurringBilling:MaxConcurrencyPerGateway:{processor}", DefaultMaxWorkers);
            var offset     = GetInt(parameters, $"RecurringBilling:GatewayWindowOffsetMinutes:{processor}", 0);

            var raw       = (int)Math.Ceiling((double)cases * avgMs / windowMs);
            var clamped   = Math.Max(minWorkers, Math.Min(maxWorkers, raw));
            var hitFloor  = clamped > raw;
            var hitCeil   = clamped < raw;

            // Estimated runtime for this gateway (minutes):
            // (cases × avgLatencyMs) / (workers × 1000 [to seconds] × 60 [to minutes])
            // equivalently: (cases × avgMs) / (workers × 60_000)
            double gatewayMinutes = (cases * avgMs) / ((double)clamped * 60_000.0);
            if (gatewayMinutes > worstGatewayMinutes)
                worstGatewayMinutes = gatewayMinutes;

            if (offset > maxOffset)
                maxOffset = offset;

            rows.Add(new GatewayPreviewRow
            {
                Processor     = processor.ToString(),
                CasesAssigned = cases,
                AvgLatencyMs  = (long)Math.Round(avgMs),
                WorkersNeeded = clamped,
                HitFloor      = hitFloor,
                HitCeiling    = hitCeil
            });
        }

        // Completion = worst gateway runtime + largest window offset
        double estimatedMinutes = worstGatewayMinutes + maxOffset;
        bool   withinTarget     = estimatedMinutes <= targetWindowHours * 60.0;

        return Result<RecurringBillingPlannerPreview>.Success(new RecurringBillingPlannerPreview
        {
            AsOfUtc                    = now,
            PendingBills               = dueStates.Count,
            TargetWindowHours          = targetWindowHours,
            PerGateway                 = rows,
            EstimatedCompletionMinutes = Math.Round(estimatedMinutes, 2),
            WithinTarget               = withinTarget,
            Notes                      = notes
        });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static List<RecurringBillingBatchShard> PartitionIntoShards(
        List<long> sortedShardKeys,
        int workerCount,
        string createdBy,
        DateTime now,
        string batchId)
    {
        var shards = new List<RecurringBillingBatchShard>();
        if (sortedShardKeys.Count == 0) return shards;

        int keysPerShard  = (int)Math.Ceiling((double)sortedShardKeys.Count / workerCount);
        int shardIndex    = 0;

        for (int i = 0; i < sortedShardKeys.Count; i += keysPerShard)
        {
            var slice = sortedShardKeys.Skip(i).Take(keysPerShard).ToList();
            shards.Add(new RecurringBillingBatchShard
            {
                Id              = Guid.NewGuid().ToString(),
                BatchId         = batchId,
                ShardIndex      = shardIndex++,
                IdRangeStart    = slice.First(),
                IdRangeEnd      = slice.Last(),
                Status          = RecurringBillingBatchStatus.Planned,
                CasesProcessed  = 0,
                CreatedBy       = createdBy,
                CreationDate    = now,
                LastUpdateDate  = now
            });
        }

        return shards;
    }

    private static int GetInt(
        IEnumerable<Domain.Entities.General.GlobalParameter> parameters,
        string key,
        int defaultValue)
    {
        var param = parameters.FirstOrDefault(p => p.Key == key);
        return param is not null && int.TryParse(param.Value, out var v) ? v : defaultValue;
    }

    private static string GetString(
        IEnumerable<Domain.Entities.General.GlobalParameter> parameters,
        string key,
        string defaultValue)
    {
        var param = parameters.FirstOrDefault(p => p.Key == key);
        return param?.Value ?? defaultValue;
    }
}
