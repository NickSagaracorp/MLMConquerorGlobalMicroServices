using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// Runtime billing state for a single subscription that is governed by a RecurringBillingPlan.
/// One row per active subscription to a recurring-plan product.
/// Optimistic concurrency via RowVersion (inherited from AuditChangesStringKey).
/// </summary>
public class SubscriptionBillingState : AuditChangesStringKey
{
    public string MembershipSubscriptionId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public int RecurringBillingPlanId { get; set; }

    /// <summary>
    /// The enrollment/origin date of the subscription. Used for monthly-anniversary math
    /// (Travel Advantage: retry on BillingAnchorDate day-of-month in the following month).
    /// </summary>
    public DateTime BillingAnchorDate { get; set; }

    public DateTime? LastSuccessfulBillingDate { get; set; }

    /// <summary>Start date of the next billing cycle (advances on each success).</summary>
    public DateTime NextBillingDate { get; set; }

    /// <summary>
    /// 0 = this cycle has not been attempted yet (NextAttemptDate == NextBillingDate).
    /// 1..N = how many retries have been attempted in this cycle.
    /// </summary>
    public int CurrentAttemptIndex { get; set; }

    /// <summary>The actual date of the next charge attempt (may equal NextBillingDate on first try).</summary>
    public DateTime NextAttemptDate { get; set; }

    public RecurringBillingStatus Status { get; set; } = RecurringBillingStatus.Active;

    public DateTime? LastAttemptAt { get; set; }
    public string? LastAttemptOutcome { get; set; }
    public string? LastFailureReason { get; set; }

    /// <summary>
    /// Monotonically increasing surrogate key used for shard range partitioning in the
    /// high-volume pipeline (§10.3). Set by the database on INSERT via IDENTITY(1,1).
    /// Shard boundaries are expressed as [ShardKey_start, ShardKey_end] ranges, providing
    /// a stable, contiguous, numeric space to partition across without cursor drift.
    /// </summary>
    public long ShardKey { get; set; }

    // Navigation
    public RecurringBillingPlan? Plan { get; set; }
}
