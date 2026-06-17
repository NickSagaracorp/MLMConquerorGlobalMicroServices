using MLMConquerorGlobalEdition.Repository.Grid;
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
    /// Server-side grid read (search · per-column filter · sort · page) over the
    /// viewer's whole binary subtree, so the grid finds matches on any page.
    /// </summary>
    Task<PagedResult<DualTeamMyTeamMemberView>> GetMyTeamGridAsync(
        string memberId, GridDataRequest request, CancellationToken ct = default);

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

    /// <summary>
    /// Left/right leg point totals for a member's binary position + the per-leg cap toward the
    /// member's next rank. Reads the denormalised <c>DualTeamTree.LeftLegPoints/RightLegPoints</c>
    /// (maintained by the placement engine; the same values rank qualification uses) — O(1),
    /// never an O(downline) subtree recompute. Single source for both Admin and BizCenter
    /// dual-tree/stats endpoints.
    /// </summary>
    Task<DualTreeStatsView> GetDualTreeStatsAsync(string memberId, CancellationToken ct = default);

    /// <summary>
    /// Find members in <paramref name="rootMemberId"/>'s binary subtree whose name or member id
    /// matches <paramref name="term"/>. Each hit carries its path from the root (so the
    /// visualizer can drill to the branch and highlight it), the leg it sits on and its depth.
    /// Capped at <paramref name="take"/>; shallowest matches first. Empty term / no match → empty.
    /// </summary>
    Task<List<DualTreeSearchMatchView>> SearchBinarySubtreeAsync(
        string rootMemberId, string? term, int take = 25, CancellationToken ct = default);

    /// <summary>
    /// The deepest node on <paramref name="rootMemberId"/>'s <paramref name="side"/> leg (for the
    /// "jump to deepest left/right" navigation arrows), with its path from the root. Null when
    /// that leg is empty.
    /// </summary>
    Task<DualTreeNavTargetView?> GetDeepestNodeAsync(
        string rootMemberId, Domain.Enums.TreeSide side, CancellationToken ct = default);
}
