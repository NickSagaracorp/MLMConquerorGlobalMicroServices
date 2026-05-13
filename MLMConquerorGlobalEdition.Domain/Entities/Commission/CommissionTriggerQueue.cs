using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Commission;

/// <summary>
/// Coordination table between the billing charge worker (Stage 2 — §10.4) and the
/// CommissionEngine for FSB (Fast Start Bonus) and Boost Bonus second-half triggers.
///
/// On a successful Activated billing event, the charge worker inserts a row here.
/// The DownstreamTriggersJob (Stage 4 — §10.6) consumes unprocessed rows and
/// enqueues the appropriate CommissionEngine Hangfire jobs.
///
/// Kept intentionally minimal — no calculation logic here, only a coordination signal.
/// </summary>
public class CommissionTriggerQueue : AuditChangesLongKey
{
    /// <summary>The batch this trigger belongs to.</summary>
    public string BatchId { get; set; } = string.Empty;

    /// <summary>The member whose successful renewal triggered an FSB or Boost evaluation.</summary>
    public string MemberId { get; set; } = string.Empty;

    /// <summary>The order that was created for the successful renewal.</summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// Which commission type to trigger: "FastStartBonus" or "BoostBonus".
    /// These map to the CommissionEngine's existing handlers.
    /// </summary>
    public string TriggerType { get; set; } = string.Empty;

    public bool IsProcessed { get; set; } = false;

    public DateTime? ProcessedAt { get; set; }

    public string? ErrorMessage { get; set; }
}
