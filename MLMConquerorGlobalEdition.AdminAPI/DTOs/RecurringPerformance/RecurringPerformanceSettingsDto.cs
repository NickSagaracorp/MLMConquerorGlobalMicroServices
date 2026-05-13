using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.RecurringPerformance;

/// <summary>
/// Structured payload for GET + PUT /api/v1/admin/billing/recurring-performance/settings.
/// Groups all §10.7 high-volume tunables.
/// </summary>
public class RecurringPerformanceSettingsDto
{
    public WindowSettingsDto                Window                   { get; init; } = new();
    public List<GatewayPerformanceRowDto>   PerGateway               { get; init; } = new();
    public int                              LatencySamplingDays      { get; init; }
    public string                           CascadeStrategy          { get; init; } = string.Empty;
    public string                           AggregatorTriggerMode    { get; init; } = string.Empty;

    /// <summary>GET-only. Legal values for the CascadeStrategy dropdown.</summary>
    public List<string>                     SupportedCascadeStrategies       { get; init; } = new();

    /// <summary>GET-only. Legal values for the AggregatorTriggerMode dropdown.</summary>
    public List<string>                     SupportedAggregatorTriggerModes  { get; init; } = new();
}

public class WindowSettingsDto
{
    public int    TargetCompletionWindowHours { get; init; }
    public string BatchStartTimeUtc          { get; init; } = string.Empty;
}

public class GatewayPerformanceRowDto
{
    /// <summary>CardProcessor enum name string, e.g. "NmiSpreedly".</summary>
    public string Processor          { get; init; } = string.Empty;
    public int    MinWorkers         { get; init; }
    public int    MaxConcurrency     { get; init; }
    public int    WindowOffsetMinutes { get; init; }
}

/// <summary>
/// Body shape for PUT /api/v1/admin/billing/recurring-performance/settings.
/// No supported* arrays — those are GET-only.
/// </summary>
public class UpdateRecurringPerformanceSettingsRequest
{
    public WindowSettingsDto              Window                { get; init; } = new();
    public List<GatewayPerformanceRowDto> PerGateway            { get; init; } = new();
    public int                            LatencySamplingDays   { get; init; }
    public string                         CascadeStrategy       { get; init; } = string.Empty;
    public string                         AggregatorTriggerMode { get; init; } = string.Empty;
}
