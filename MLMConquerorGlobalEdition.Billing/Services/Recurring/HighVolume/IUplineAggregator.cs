using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;

public class UplineAggregatorResult
{
    public int EventsApplied      { get; init; }
    public int UplineMembersUpdated { get; init; }
}

/// <summary>
/// Stage 3 of the high-volume pipeline (BILLING-RULES §10.5).
///
/// Reads all Queued <see cref="PointDeltaEvent"/> rows for the given batch.
/// For each event, walks the downline member's enrollment-tree HierarchyPath
/// to identify every upline. Reduces all events into a single net delta
/// per upline member (Dictionary&lt;uplineMemberId, NetDelta&gt;), then applies
/// one bulk UPDATE per upline to MemberStatisticEntity.
///
/// This design eliminates per-charge tree contention: instead of N workers
/// each trying to increment the same upline's point counter, Stage 3 runs
/// once after all charge workers have finished and applies one write per upline.
///
/// Idempotency: once an event is marked Applied it is never re-processed.
/// If the job crashes mid-batch it resumes from the remaining Queued rows.
/// </summary>
public interface IUplineAggregator
{
    Task<Result<UplineAggregatorResult>> AggregateAsync(
        string batchId,
        CancellationToken ct = default);
}
