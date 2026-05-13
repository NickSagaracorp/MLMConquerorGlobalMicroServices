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

    /// <summary>
    /// Three-row "Dual Team Members" feed for the Residuals page: the viewer
    /// (root) plus their left and right binary gateway children. Each row
    /// reports the leg's cumulative points and donut eligibility against the
    /// viewer's current and next rank per-leg caps. Returns 1 row when the
    /// viewer has no binary node yet, 2 rows when only one leg is filled.
    /// </summary>
    Task<List<DualLegRowView>> GetResidualLegsAsync(
        string memberId, CancellationToken ct = default);

    /// <summary>
    /// Last <paramref name="months"/> months of dual-team leg points sourced
    /// from <c>MemberStatisticHistory</c>. Always returns exactly that many
    /// buckets in chronological order — months with no snapshot yet are
    /// filled with zero so the chart's x-axis stays consistent. The most
    /// recent bucket (current month) reflects the live <c>DualTeamTree</c>
    /// values rather than the snapshot, since the snapshot is end-of-day
    /// (yesterday) and would lag behind today's placements/orders.
    /// </summary>
    Task<List<DualLegMonthlyPointView>> GetDualTeamHistoryAsync(
        string memberId, int months, CancellationToken ct = default);
}
