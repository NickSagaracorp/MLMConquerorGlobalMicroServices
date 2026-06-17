using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Member;

/// <summary>
/// Sprint-16 — eventual-consistency queue row for ancestor MemberStatistics increments.
///
/// Phase-3 of the signup pipeline used to walk the ancestor chain and emit one
/// SQL MERGE per upline (76 round-trips for a 76-deep tree). Under 350-concurrent
/// signups the per-ancestor MERGE serialised on shared rows near the root and
/// pushed mean signup latency to ~16s. We now enqueue one
/// <see cref="MemberStatisticDelta"/> row per upline in a single batch insert and
/// let <c>ApplyMemberStatisticDeltasJob</c> roll the deltas into
/// <c>MemberStatistics</c> on a recurring cadence (groups by MemberId so 350
/// signups under one upline collapse into one MERGE per cycle).
///
/// Inherits <see cref="AuditChangesLongKey"/> — high-volume table, long PK,
/// insert-then-mark-applied semantics, no soft-delete or row-version overhead.
/// </summary>
public class MemberStatisticDelta : AuditChangesLongKey
{
    /// <summary>The upline member whose <c>MemberStatistics</c> row this delta will adjust.</summary>
    public string MemberId { get; set; } = string.Empty;

    public int EnrollmentPointsDelta { get; set; }
    public int EnrollmentTeamSizeDelta { get; set; }
    public int QualifiedSponsoredMembersDelta { get; set; }

    /// <summary>Flipped to <c>true</c> by the apply job after the MERGE commits successfully.</summary>
    public bool IsApplied { get; set; }

    public DateTime? AppliedAt { get; set; }

    /// <summary>The newly-completed member whose signup produced this delta — used for diagnostics + audit traces.</summary>
    public string SourceMemberId { get; set; } = string.Empty;
}
