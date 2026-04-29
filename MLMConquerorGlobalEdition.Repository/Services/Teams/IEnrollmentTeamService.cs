using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// Single source of truth for enrollment-tree queries. Used by both BizCenter
/// (member's own view) and Admin (member profile drill-down). Do NOT duplicate
/// any of these queries elsewhere.
/// </summary>
public interface IEnrollmentTeamService
{
    Task<PagedResult<EnrollmentMyTeamMemberView>> GetMyTeamAsync(
        string memberId, int page, int pageSize,
        string? search, DateTime? from, DateTime? to,
        CancellationToken ct = default);

    Task<EnrollmentBranchesView> GetBranchesAsync(
        string memberId, int page, int pageSize, string? search,
        CancellationToken ct = default);

    Task<BranchDetailView?> GetBranchDetailAsync(
        string branchMemberId, CancellationToken ct = default);

    Task<PagedResult<EnrollmentCustomerView>> GetCustomersAsync(
        string memberId, int page, int pageSize, string? search,
        CancellationToken ct = default);

    Task<EnrollmentVisualizerStatsView> GetVisualizerStatsAsync(
        string memberId, CancellationToken ct = default);

    Task<List<EnrollmentVisualizerChildView>> GetVisualizerChildrenAsync(
        string parentMemberId, CancellationToken ct = default);
}
