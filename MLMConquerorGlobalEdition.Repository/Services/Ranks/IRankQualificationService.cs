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
}
