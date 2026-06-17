using MLMConquerorGlobalEdition.Domain.Entities.Rank;

namespace MLMConquerorGlobalEdition.Repository.Services.Ranks;

/// <summary>
/// THE single authority for rank qualification. EvaluateRankHandler and
/// RankComputationService both delegate here — no qualification logic anywhere else.
/// </summary>
public interface IRankQualificationService
{
    /// <summary>The universal gate (§2.3): resolves PCP + sponsored count internally.</summary>
    Task<bool> MeetsUniversalGateAsync(string memberId, CancellationToken ct = default);

    /// <summary>Full qualification for one rank: universal gate + every RankRequirement axis.</summary>
    Task<RankQualificationResult> QualifiesForRankAsync(
        string memberId, RankRequirement requirement, CancellationToken ct = default);

    /// <summary>
    /// Batched form of <see cref="QualifiesForRankAsync"/>: loads the member's qualification
    /// inputs (gate, dual-team legs, enrollment branches, personal points, external members,
    /// sales volume) ONCE and evaluates every supplied requirement against the same snapshot.
    /// Returns each requirement paired with its qualification result, preserving the input order.
    /// Use this when evaluating a member against multiple ranks in one call (e.g., the rank
    /// evaluation sweep) to avoid N round-trips to the database.
    /// </summary>
    Task<IReadOnlyList<(RankRequirement Requirement, RankQualificationResult Result)>>
        QualifiesForAllRanksAsync(
            string memberId,
            IReadOnlyList<RankRequirement> requirements,
            CancellationToken ct = default);
}
