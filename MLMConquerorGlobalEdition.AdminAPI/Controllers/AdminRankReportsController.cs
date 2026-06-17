using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks;
using MLMConquerorGlobalEdition.AdminAPI.Features.Ranks.GetRankFirstAchievements;
using MLMConquerorGlobalEdition.AdminAPI.Features.Ranks.GetRankSeniorityCandidates;
using MLMConquerorGlobalEdition.AdminAPI.Features.Ranks.GrantRankSeniorityBonus;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/ranks/reports")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminRankReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminRankReportsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("first-achievements")]
    public async Task<IActionResult> FirstAchievements(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] int? rankId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var r = await _mediator.Send(new GetRankFirstAchievementsQuery(year, month, rankId, page, pageSize), ct);
        return r.IsSuccess
            ? Ok(ApiResponse<PagedResult<RankFirstAchievementRowDto>>.Ok(r.Value!))
            : BadRequest(ApiResponse<object>.Fail(r.ErrorCode!, r.Error!));
    }

    [HttpGet("seniority")]
    public async Task<IActionResult> Seniority(
        [FromQuery] int? rankId,
        [FromQuery] int minDays = 14,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var r = await _mediator.Send(new GetRankSeniorityCandidatesQuery(rankId, minDays, page, pageSize), ct);
        return r.IsSuccess
            ? Ok(ApiResponse<PagedResult<RankSeniorityRowDto>>.Ok(r.Value!))
            : BadRequest(ApiResponse<object>.Fail(r.ErrorCode!, r.Error!));
    }

    public record GrantSeniorityBody(string MemberId, int RankDefinitionId);

    [HttpPost("seniority/grant")]
    public async Task<IActionResult> GrantSeniority(
        [FromBody] GrantSeniorityBody body,
        CancellationToken ct = default)
    {
        var r = await _mediator.Send(new GrantRankSeniorityBonusCommand(body.MemberId, body.RankDefinitionId), ct);
        return r.IsSuccess
            ? Ok(ApiResponse<string>.Ok(r.Value!))
            : BadRequest(ApiResponse<object>.Fail(r.ErrorCode!, r.Error!));
    }
}
