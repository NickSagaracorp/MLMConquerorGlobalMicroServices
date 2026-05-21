using MLMConquerorGlobalEdition.Domain.Entities.Rank;

namespace MLMConquerorGlobalEdition.Repository.Services.Ranks;

/// <summary>Points contributed by one direct-sponsorship branch.</summary>
public sealed record EnrollmentBranchPoints(string ChildMemberId, int BranchPoints);

/// <summary>
/// Single source of truth for Enrollment Team (ET) points. ET = the sum of the
/// points of every member in the enrollment downline. No other class may
/// re-implement this — all consumers depend on this interface.
/// </summary>
public interface IEnrollmentTeamPointsService
{
    /// <summary>Points per direct-sponsorship branch (child's EnrollmentPoints).</summary>
    Task<IReadOnlyList<EnrollmentBranchPoints>> GetEnrollmentBranchPointsAsync(
        string memberId, CancellationToken ct = default);

    /// <summary>Flat sum of the whole enrollment downline (excludes the member's own points).</summary>
    Task<int> GetRawEnrollmentTeamPointsAsync(string memberId, CancellationToken ct = default);

    /// <summary>
    /// Branch points after the per-branch cap (MaxEnrollmentTeamPointsPerBranch × EnrollmentTeam),
    /// then capped at the rank's EnrollmentTeam threshold. Returns 0 when the requirement has no
    /// ET dimension (EnrollmentTeam &lt;= 0).
    /// </summary>
    Task<int> GetEligibleEnrollmentTeamPointsAsync(
        string memberId, RankRequirement requirement, CancellationToken ct = default);

    /// <summary>
    /// Source-of-truth recompute: walk the full genealogy subtree (including the member
    /// themselves) and re-sum <c>QualificationPoins</c> from Completed orders.
    /// Returns the correct value for <see cref="MLMConquerorGlobalEdition.Domain.Entities.Member.MemberStatisticEntity.EnrollmentPoints"/>
    /// which is defined as own points + every downline member's points.
    /// </summary>
    Task<int> RecomputeEnrollmentPointsAsync(string memberId, CancellationToken ct = default);
}
