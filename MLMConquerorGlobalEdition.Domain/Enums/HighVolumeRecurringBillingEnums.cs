namespace MLMConquerorGlobalEdition.Domain.Enums;

/// <summary>Status of a RecurringBillingBatch or RecurringBillingBatchShard.</summary>
public enum RecurringBillingBatchStatus
{
    /// <summary>Batch/shard has been planned but workers have not started yet.</summary>
    Planned = 1,

    /// <summary>Workers are actively processing this batch/shard.</summary>
    InProgress = 2,

    /// <summary>All cases in this batch/shard completed successfully.</summary>
    Done = 3,

    /// <summary>The batch/shard encountered a terminal error.</summary>
    Failed = 4
}

/// <summary>
/// What type of point movement a PointDeltaEvent represents.
/// Positive = member just became Active (renewed); Negative = member's subscription deactivated.
/// </summary>
public enum PointDeltaEventType
{
    /// <summary>
    /// A subscription renewed successfully — add the contribution to upline stats.
    /// DualTeamDelta and EnrollmentDelta are positive.
    /// </summary>
    Activated = 1,

    /// <summary>
    /// A subscription lapsed or was stopped — subtract the contribution from upline stats.
    /// DualTeamDelta and EnrollmentDelta are negative (or zero).
    /// </summary>
    Deactivated = 2
}

/// <summary>Processing status of a PointDeltaEvent row.</summary>
public enum PointDeltaEventStatus
{
    /// <summary>Emitted by a charge worker; not yet processed by the upline aggregator.</summary>
    Queued = 1,

    /// <summary>The aggregator has applied this event's delta to the upline(s) and committed.</summary>
    Applied = 2,

    /// <summary>The aggregator attempted to apply this event but encountered an error.</summary>
    Failed = 3
}
