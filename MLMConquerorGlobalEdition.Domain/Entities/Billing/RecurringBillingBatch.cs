using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// Planning record for a single high-volume recurring-billing run (§10.3).
/// One batch is created per run date by RecurringBillingPlanningJob.
/// A batch covers one CardProcessor at a time (one batch per processor per day).
/// </summary>
public class RecurringBillingBatch : AuditChangesStringKey
{
    /// <summary>The calendar date this batch covers (UTC date portion only).</summary>
    public DateTime RunDate { get; set; }

    /// <summary>Which downstream processor this batch is charging through.</summary>
    public CardProcessor Gateway { get; set; }

    /// <summary>
    /// Number of worker shards allocated by the planner (§10.3 worker-count formula).
    /// </summary>
    public int WorkerCount { get; set; }

    /// <summary>UTC datetime when the first charge worker is expected to start.</summary>
    public DateTime ScheduledStartTime { get; set; }

    /// <summary>Total number of SubscriptionBillingState rows this batch covers.</summary>
    public int CaseCount { get; set; }

    public RecurringBillingBatchStatus Status { get; set; } = RecurringBillingBatchStatus.Planned;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    // Navigation
    public ICollection<RecurringBillingBatchShard> Shards { get; set; } = new List<RecurringBillingBatchShard>();
}
