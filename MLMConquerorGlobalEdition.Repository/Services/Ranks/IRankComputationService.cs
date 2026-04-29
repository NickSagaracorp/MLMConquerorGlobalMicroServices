namespace MLMConquerorGlobalEdition.Repository.Services.Ranks;

/// <summary>
/// Single source of truth for rank-related computations. All controllers and
/// handlers — BizCenter, Admin, Engine — must use this service so a fix in one
/// place propagates everywhere. Do NOT duplicate the rank-qualification or
/// thresholds logic anywhere else.
/// </summary>
public interface IRankComputationService
{
    /// <summary>
    /// Returns the unified rank summary for a member: live current rank
    /// (computed from actual points), next rank, lifetime rank, and the
    /// member's accumulated stats. Returns an empty summary (with the
    /// supplied <paramref name="memberId"/> set) if the member has no stats yet.
    /// </summary>
    Task<RankSummaryDto> GetSummaryAsync(string memberId, CancellationToken ct = default);
}
