using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;

public class RecurringBillingPlannerResult
{
    public int BatchesCreated { get; init; }
    public int TotalCases     { get; init; }
    public int TotalShards    { get; init; }
}

/// <summary>
/// Dry-run projection computed by PreviewAsync.
/// No database rows are written; all fields are computed in memory.
/// </summary>
public class RecurringBillingPlannerPreview
{
    public DateTime                       AsOfUtc            { get; init; }
    public int                            PendingBills       { get; init; }
    public int                            TargetWindowHours  { get; init; }
    public List<GatewayPreviewRow>        PerGateway         { get; init; } = new();
    public double                         EstimatedCompletionMinutes { get; init; }
    public bool                           WithinTarget       { get; init; }
    public List<string>                   Notes              { get; init; } = new();
}

public class GatewayPreviewRow
{
    public string Processor       { get; init; } = string.Empty;
    public int    CasesAssigned   { get; init; }
    public long   AvgLatencyMs    { get; init; }
    public int    WorkersNeeded   { get; init; }
    public bool   HitFloor        { get; init; }
    public bool   HitCeiling      { get; init; }
}

/// <summary>
/// Stage 1 of the high-volume pipeline (BILLING-RULES §10.3).
/// Counts due cases for today, routes them in dry mode, computes worker counts,
/// and writes RecurringBillingBatch + RecurringBillingBatchShard rows.
/// Idempotent for a given RunDate — returns existing plan if already created.
///
/// PreviewAsync runs the same computation as PlanAsync but does NOT write any rows.
/// It is used by the admin "recurring-performance/preview" endpoint.
/// </summary>
public interface IRecurringBillingPlanner
{
    Task<Result<RecurringBillingPlannerResult>> PlanAsync(
        DateTime runDate,
        CancellationToken ct = default);

    /// <summary>
    /// Dry-run: computes the planning projection for <paramref name="runDate"/>
    /// without writing any RecurringBillingBatch or RecurringBillingBatchShard rows.
    /// </summary>
    Task<Result<RecurringBillingPlannerPreview>> PreviewAsync(
        DateTime runDate,
        CancellationToken ct = default);
}
