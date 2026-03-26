using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetAllTeamMembers;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetDualTree;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetEnrollmentTeam;
using MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetTeamMembers;
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
}
