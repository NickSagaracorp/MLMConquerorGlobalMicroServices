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
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

[ApiController]
[Route("api/v1/bizcenter/team")]
[Authorize]
public class TeamController : ControllerBase
{
    private readonly IMediator _mediator;

    public TeamController(IMediator mediator) => _mediator = mediator;

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
