using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Commission;

/// <summary>
/// Records the point contribution change emitted by a charge worker (§10.4) when a
/// subscription's billing state transitions (Activated = renewed; Deactivated = lapsed).
///
/// The upline aggregator (§10.5) reads Queued rows for a batch, reduces them to a
/// net delta per upline member, and applies one UPDATE per upline — eliminating the
/// tree-contention problem of per-charge upline updates.
///
/// Lifecycle: Queued → Applied (same transaction as the upline UPDATE).
/// </summary>
public class PointDeltaEvent : AuditChangesLongKey
{
    /// <summary>The RecurringBillingBatch this event belongs to.</summary>
    public string BatchId { get; set; } = string.Empty;

    /// <summary>The order that triggered this event (from RecurringBillingAttempt).</summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// The member whose subscription changed (the emitter / downline member).
    /// NOT the upline — the aggregator walks the upline from this member's HierarchyPath.
    /// </summary>
    public string MemberId { get; set; } = string.Empty;

    public string? ProductId { get; set; }

    public PointDeltaEventType EventType { get; set; }

    /// <summary>
    /// Signed delta to apply to each upline's DualTeamPoints.
    /// Positive on Activated; negative (= stored MembershipSubscription.DualTeamContribution negated) on Deactivated.
    /// </summary>
    public int DualTeamDelta { get; set; }

    /// <summary>
    /// Signed delta to apply to each upline's EnrollmentPoints.
    /// Positive on Activated; negative on Deactivated.
    /// </summary>
    public int EnrollmentDelta { get; set; }

    /// <summary>
    /// Signed delta to apply to the member's own PersonalPoints.
    /// Positive on Activated; negative on Deactivated.
    /// </summary>
    public int PersonalDelta { get; set; }

    public DateTime OccurredAt { get; set; }

    public PointDeltaEventStatus Status { get; set; } = PointDeltaEventStatus.Queued;

    public DateTime? AppliedAt { get; set; }

    public string? FailureReason { get; set; }
}
