using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// A contiguous shard of a RecurringBillingBatch (§10.3).
/// Each shard covers a non-overlapping range of SubscriptionBillingState.ShardKey values
/// and is processed by exactly one ChargeWorkerJob instance.
/// </summary>
public class RecurringBillingBatchShard : AuditChangesStringKey
{
    public string BatchId { get; set; } = string.Empty;

    /// <summary>Zero-based index within the batch (0 = first shard).</summary>
    public int ShardIndex { get; set; }

    /// <summary>
    /// Inclusive lower bound of the SubscriptionBillingState.ShardKey range this shard covers.
    /// </summary>
    public long IdRangeStart { get; set; }

    /// <summary>
    /// Inclusive upper bound of the SubscriptionBillingState.ShardKey range this shard covers.
    /// </summary>
    public long IdRangeEnd { get; set; }

    /// <summary>
    /// Identifier of the worker that claimed this shard (Hangfire job id or worker key).
    /// Set when the worker starts processing; used for resumability.
    /// </summary>
    public string? AssignedWorkerKey { get; set; }

    public RecurringBillingBatchStatus Status { get; set; } = RecurringBillingBatchStatus.Planned;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>Count of SubscriptionBillingState rows successfully processed in this shard.</summary>
    public int CasesProcessed { get; set; }

    public string? Notes { get; set; }

    // Navigation
    public RecurringBillingBatch? Batch { get; set; }
}
