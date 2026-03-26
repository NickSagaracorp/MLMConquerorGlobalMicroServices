using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.RankRequirements;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks;
using MLMConquerorGlobalEdition.AdminAPI.Features.RankRequirements.CreateRankRequirement;
using MLMConquerorGlobalEdition.AdminAPI.Features.RankRequirements.DeleteRankRequirement;
using MLMConquerorGlobalEdition.AdminAPI.Features.RankRequirements.GetRankDashboard;
using MLMConquerorGlobalEdition.AdminAPI.Features.RankRequirements.GetRankRequirements;
using MLMConquerorGlobalEdition.AdminAPI.Features.RankRequirements.UpdateRankRequirement;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/rank-definitions")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class RankRequirementsController : ControllerBase
{
    private readonly IMediator _mediator;

    public RankRequirementsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetRankDashboardQuery(), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<RankDashboardDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<RankDashboardDto>.Ok(result.Value!));
    }

    [HttpGet("{rankId:int}/requirements")]
    public async Task<IActionResult> GetRequirements(int rankId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetRankRequirementsQuery(rankId), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<IEnumerable<RankRequirementDto>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<IEnumerable<RankRequirementDto>>.Ok(result.Value!));
    }

    [HttpPost("{rankId:int}/requirements")]
    public async Task<IActionResult> CreateRequirement(
        int rankId,
        [FromBody] CreateRankRequirementDto request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CreateRankRequirementCommand(rankId, request), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<int>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<int>.Ok(result.Value!, "Rank requirement created."));
    }

    [HttpPut("{rankId:int}/requirements/{id:int}")]
    public async Task<IActionResult> UpdateRequirement(
        int rankId,
        int id,
        [FromBody] CreateRankRequirementDto request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new UpdateRankRequirementCommand(rankId, id, request), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<bool>.Ok(true, "Rank requirement updated."));
    }

    [HttpDelete("{rankId:int}/requirements/{id:int}")]
    public async Task<IActionResult> DeleteRequirement(
        int rankId,
        int id,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DeleteRankRequirementCommand(rankId, id), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<bool>.Ok(true, "Rank requirement deleted."));
    }
}
