using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.General;

/// <summary>
/// Key/value store for system-wide tuneable parameters.
/// Values are stored as strings; callers are responsible for parsing to the correct type.
/// </summary>
public class GlobalParameter : AuditChangesIntKey
{
    /// <summary>Unique parameter key, e.g. "DailyResidualConsolidationMinimum".</summary>
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Description { get; set; }
}
