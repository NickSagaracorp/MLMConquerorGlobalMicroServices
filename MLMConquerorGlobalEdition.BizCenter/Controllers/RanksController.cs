using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Ranks;
using MLMConquerorGlobalEdition.BizCenter.Features.Ranks.GetRankDashboard;
using MLMConquerorGlobalEdition.BizCenter.Features.Ranks.GetRankHistory;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

[ApiController]
[Route("api/v1/bizcenter/ranks")]
[Authorize]
public class RanksController : ControllerBase
{
    private readonly IMediator _mediator;

    public RanksController(IMediator mediator) => _mediator = mediator;

    /// <summary>GET /api/v1/bizcenter/ranks/dashboard — current rank and stats</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetRankDashboard(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRankDashboardQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<RankDashboardDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<RankDashboardDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/ranks/history — paginated rank history</summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetRankHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetRankHistoryQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<RankHistoryDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<RankHistoryDto>>.Ok(result.Value!));
    }
}
