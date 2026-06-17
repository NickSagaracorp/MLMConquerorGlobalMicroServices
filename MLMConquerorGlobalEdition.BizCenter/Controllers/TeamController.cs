using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetAllTeamMembers;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetDualTree;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetEnrollmentTeam;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetEnrollmentMyTeam;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetBranchDetail;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetEnrollmentBranches;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetEnrollmentCustomers;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetTeamMembers;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetVisualizerStats;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetVisualizerChildren;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetDualTreeNode;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetDualTreeStats;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetDualTeamMyTeam;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

[ApiController]
[Route("api/v1/bizcenter/team")]
[Authorize]
public class TeamController : ControllerBase
{
    private readonly IMediator              _mediator;
    private readonly IDualTeamService       _dualTeam;
    private readonly IEnrollmentTeamService _enrollment;
    private readonly ICurrentUserService    _currentUser;

    public TeamController(
        IMediator              mediator,
        IDualTeamService       dualTeam,
        IEnrollmentTeamService enrollment,
        ICurrentUserService    currentUser)
    {
        _mediator    = mediator;
        _dualTeam    = dualTeam;
        _enrollment  = enrollment;
        _currentUser = currentUser;
    }

    // ─── Server-side grid reads (search/filter/sort/page span the whole team) ──
    // These delegate straight to the shared team services — the same ones the
    // Admin controllers use — so Admin and BizCenter grids behave identically.
    // They return the service view; its JSON shape matches the existing *Dto
    // (the MediatR handlers map 1:1), so the frontend deserializes unchanged.

    /// <summary>POST /api/v1/bizcenter/team/dual-tree/my-team/grid</summary>
    [HttpPost("dual-tree/my-team/grid")]
    public async Task<IActionResult> GetDualTeamMyTeamGrid([FromBody] GridDataRequest request, CancellationToken ct = default)
    {
        var result = await _dualTeam.GetMyTeamGridAsync(_currentUser.MemberId, request, ct);
        return Ok(ApiResponse<PagedResult<DualTeamMyTeamMemberView>>.Ok(result));
    }

    /// <summary>POST /api/v1/bizcenter/team/enrollment/my-team/grid</summary>
    [HttpPost("enrollment/my-team/grid")]
    public async Task<IActionResult> GetEnrollmentMyTeamGrid([FromBody] GridDataRequest request, CancellationToken ct = default)
    {
        var result = await _enrollment.GetMyTeamGridAsync(_currentUser.MemberId, request, ct);
        return Ok(ApiResponse<PagedResult<EnrollmentMyTeamMemberView>>.Ok(result));
    }

    /// <summary>POST /api/v1/bizcenter/team/enrollment/customers/grid</summary>
    [HttpPost("enrollment/customers/grid")]
    public async Task<IActionResult> GetEnrollmentCustomersGrid([FromBody] GridDataRequest request, CancellationToken ct = default)
    {
        var result = await _enrollment.GetCustomersGridAsync(_currentUser.MemberId, request, ct);
        return Ok(ApiResponse<PagedResult<EnrollmentCustomerView>>.Ok(result));
    }

    /// <summary>GET /api/v1/bizcenter/team/enrollment — direct sponsored members</summary>
    [HttpGet("enrollment")]
    public async Task<IActionResult> GetEnrollmentTeam(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetEnrollmentTeamQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<IEnumerable<TeamMemberDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<IEnumerable<TeamMemberDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/team/dual-tree — immediate binary tree children (left + right)</summary>
    [HttpGet("dual-tree")]
    public async Task<IActionResult> GetDualTree(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDualTreeQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<IEnumerable<DualTreeMemberDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<IEnumerable<DualTreeMemberDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/team/members — paged direct sponsored members</summary>
    [HttpGet("members")]
    public async Task<IActionResult> GetTeamMembers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetTeamMembersQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<TeamMemberDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<TeamMemberDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/team/all-members — full subtree via HierarchyPath LIKE query</summary>
    [HttpGet("all-members")]
    public async Task<IActionResult> GetAllTeamMembers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAllTeamMembersQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<TeamMemberDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<TeamMemberDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/team/enrollment/my-team — full enriched enrollment team list</summary>
    [HttpGet("enrollment/my-team")]
    public async Task<IActionResult> GetEnrollmentMyTeam(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetEnrollmentMyTeamQuery(page, pageSize, search, from, to), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<EnrollmentMyTeamMemberDto>>
                .Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<EnrollmentMyTeamMemberDto>>.Ok(result.Value!));
    }

    /// <summary>
    /// GET /api/v1/bizcenter/team/dual-tree/my-team — full enriched dual-tree
    /// (binary) team list. Filters by the viewer's binary subtree, computes
    /// each descendant's Leg (Left/Right) from the gateway-node Side, and
    /// joins membership/rank/points data per member. Cached for 5 minutes per
    /// (member, page, filter); pass <c>?bypassCache=true</c> to force a fresh
    /// read when the user clicks the page's refresh button.
    /// </summary>
    [HttpGet("dual-tree/my-team")]
    public async Task<IActionResult> GetDualTeamMyTeam(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] bool bypassCache = false,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetDualTeamMyTeamQuery(page, pageSize, search, from, to, bypassCache), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<DualTeamMyTeamMemberDto>>
                .Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<DualTeamMyTeamMemberDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/team/enrollment/branches — direct sponsored branches with points and rank eligibility</summary>
    [HttpGet("enrollment/branches")]
    public async Task<IActionResult> GetEnrollmentBranches(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetEnrollmentBranchesQuery(search, page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<EnrollmentBranchesResultDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<EnrollmentBranchesResultDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/team/enrollment/branches/{branchMemberId}/detail — full downline of a branch</summary>
    [HttpGet("enrollment/branches/{branchMemberId}/detail")]
    public async Task<IActionResult> GetBranchDetail(string branchMemberId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBranchDetailQuery(branchMemberId), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<BranchDetailDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<BranchDetailDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/team/enrollment/customers — ExternalMember type only</summary>
    [HttpGet("enrollment/customers")]
    public async Task<IActionResult> GetEnrollmentCustomers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetEnrollmentCustomersQuery(page, pageSize, search), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<EnrollmentCustomerDto>>
                .Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<EnrollmentCustomerDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/team/dual-tree/node/{nodeMemberId} — node + immediate L/R children for the binary tree visualizer</summary>
    [HttpGet("dual-tree/node/{nodeMemberId}")]
    public async Task<IActionResult> GetDualTreeNode(string nodeMemberId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDualTreeNodeQuery(nodeMemberId), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<DualTreeNodeDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<DualTreeNodeDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/team/dual-tree/legs — root + L/R gateway
    /// children with cumulative leg points and donut eligibility for the
    /// Residuals page Dual Team Members table. Distinct from /team/members
    /// (which still serves the full sponsored downline for token pickers).</summary>
    [HttpGet("dual-tree/legs")]
    public async Task<IActionResult> GetDualTreeLegs(
        [FromServices] MLMConquerorGlobalEdition.Repository.Services.Teams.IDualTeamService dualTeamService,
        [FromServices] MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var rows = await dualTeamService.GetResidualLegsAsync(currentUser.MemberId, ct);
        return Ok(ApiResponse<List<MLMConquerorGlobalEdition.Repository.Services.Teams.DualLegRowView>>.Ok(rows));
    }

    /// <summary>GET /api/v1/bizcenter/team/dual-tree/search?term=&amp;take=25 — find nodes in the
    /// viewer's binary subtree by name or member id. Each hit carries its path from the root so the
    /// visualizer can open that branch and highlight the match. Shares IDualTeamService with Admin.</summary>
    [HttpGet("dual-tree/search")]
    public async Task<IActionResult> SearchDualTree(
        [FromServices] MLMConquerorGlobalEdition.Repository.Services.Teams.IDualTeamService dualTeamService,
        [FromServices] MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService currentUser,
        [FromQuery] string? term = null,
        [FromQuery] int take = 25,
        CancellationToken ct = default)
    {
        var hits = await dualTeamService.SearchBinarySubtreeAsync(currentUser.MemberId, term, take, ct);
        return Ok(ApiResponse<List<MLMConquerorGlobalEdition.Repository.Services.Teams.DualTreeSearchMatchView>>.Ok(hits));
    }

    /// <summary>GET /api/v1/bizcenter/team/dual-tree/deepest?side=Left|Right — the deepest node on
    /// the viewer's given leg, with its path from the root, for the "jump to deepest left/right"
    /// navigation arrows. Returns null data when that leg is empty. Shares IDualTeamService with Admin.</summary>
    [HttpGet("dual-tree/deepest")]
    public async Task<IActionResult> GetDualTreeDeepest(
        [FromServices] MLMConquerorGlobalEdition.Repository.Services.Teams.IDualTeamService dualTeamService,
        [FromServices] MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService currentUser,
        [FromQuery] Domain.Enums.TreeSide side,
        CancellationToken ct = default)
    {
        var target = await dualTeamService.GetDeepestNodeAsync(currentUser.MemberId, side, ct);
        return Ok(ApiResponse<MLMConquerorGlobalEdition.Repository.Services.Teams.DualTreeNavTargetView?>.Ok(target));
    }

    /// <summary>GET /api/v1/bizcenter/team/dual-tree/history?months=6 — last N
    /// monthly snapshots of L/R leg points for the Total Dual Team Points
    /// trend chart on the Residuals page. The latest bucket reflects live
    /// DualTeamTree values rather than yesterday's snapshot.</summary>
    [HttpGet("dual-tree/history")]
    public async Task<IActionResult> GetDualTreeHistory(
        [FromServices] MLMConquerorGlobalEdition.Repository.Services.Teams.IDualTeamService dualTeamService,
        [FromServices] MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService currentUser,
        [FromQuery] int months = 6,
        CancellationToken ct = default)
    {
        var rows = await dualTeamService.GetDualTeamHistoryAsync(currentUser.MemberId, months, ct);
        return Ok(ApiResponse<List<MLMConquerorGlobalEdition.Repository.Services.Teams.DualLegMonthlyPointView>>.Ok(rows));
    }

    /// <summary>GET /api/v1/bizcenter/team/dual-tree/stats/{nodeMemberId} — left/right leg points for a member's binary tree position</summary>
    [HttpGet("dual-tree/stats/{nodeMemberId}")]
    public async Task<IActionResult> GetDualTreeStats(string nodeMemberId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDualTreeStatsQuery(nodeMemberId), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<DualTreeStatsDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<DualTreeStatsDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/team/enrollment/visualizer/stats — downline status counts for the tree visualizer</summary>
    [HttpGet("enrollment/visualizer/stats")]
    public async Task<IActionResult> GetVisualizerStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetVisualizerStatsQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<EnrollmentVisualizerStatsDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<EnrollmentVisualizerStatsDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/team/enrollment/visualizer/children/{parentMemberId} — direct children for lazy tree expansion</summary>
    [HttpGet("enrollment/visualizer/children/{parentMemberId}")]
    public async Task<IActionResult> GetVisualizerChildren(string parentMemberId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetVisualizerChildrenQuery(parentMemberId), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<IEnumerable<EnrollmentVisualizerNodeDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<IEnumerable<EnrollmentVisualizerNodeDto>>.Ok(result.Value!));
    }
}
