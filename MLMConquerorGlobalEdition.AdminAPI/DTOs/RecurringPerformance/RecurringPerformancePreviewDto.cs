using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.RecurringPerformance;

/// <summary>
/// Dry-run projection returned by GET /api/v1/admin/billing/recurring-performance/preview.
/// No database rows are written; numbers are computed from the current tunables and today's
/// pending SubscriptionBillingState workload.
/// </summary>
public class RecurringPerformancePreviewDto
{
    public DateTime                      AsOfUtc            { get; init; }
    public int                           PendingBills       { get; init; }
    public int                           TargetWindowHours  { get; init; }
    public List<GatewayPreviewRowDto>    PerGateway         { get; init; } = new();
    public EstimatedCompletionDto        Estimated          { get; init; } = new();

    /// <summary>
    /// Optional informational notes (e.g. when a gateway's average latency was
    /// unavailable and a default was substituted).
    /// </summary>
    public List<string>                  Notes              { get; init; } = new();
}

public class GatewayPreviewRowDto
{
    /// <summary>CardProcessor enum name string, e.g. "NmiSpreedly".</summary>
    public string Processor       { get; init; } = string.Empty;
    public int    CasesAssigned   { get; init; }
    public long   AvgLatencyMs    { get; init; }
    public int    WorkersNeeded   { get; init; }

    /// <summary>True when WorkersNeeded was clamped up by the MinWorkers floor.</summary>
    public bool   HitFloor        { get; init; }

    /// <summary>True when WorkersNeeded was clamped down by the MaxConcurrency ceiling.</summary>
    public bool   HitCeiling      { get; init; }
}

public class EstimatedCompletionDto
{
    public double CompletionMinutes { get; init; }
    public bool   WithinTarget      { get; init; }
}
