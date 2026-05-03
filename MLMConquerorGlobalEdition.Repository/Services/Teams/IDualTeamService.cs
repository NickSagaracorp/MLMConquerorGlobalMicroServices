using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// Single source of truth for dual-team (binary-tree) queries. Used by both
/// BizCenter (member's own view) and Admin (member profile drill-down). Do
/// NOT duplicate any of these queries elsewhere.
/// </summary>
public interface IDualTeamService
{
    Task<PagedResult<DualTeamMyTeamMemberView>> GetMyTeamAsync(
        string memberId, int page, int pageSize, string? search,
        DateTime? from, DateTime? to,
        CancellationToken ct = default);
}
