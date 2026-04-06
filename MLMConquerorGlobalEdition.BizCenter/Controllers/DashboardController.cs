using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Dashboard;
using MLMConquerorGlobalEdition.BizCenter.Features.Dashboard.GetDashboardStats;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

[ApiController]
[Route("api/v1/bizcenter/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator) => _mediator = mediator;

    /// <summary>GET /api/v1/bizcenter/dashboard/stats — aggregated KPI data for the member dashboard.</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDashboardStatsQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<DashboardStatsDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<DashboardStatsDto>.Ok(result.Value!));
    }
}
