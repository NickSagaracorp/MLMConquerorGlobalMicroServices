using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Dashboards;
using MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetCeoDashboard;
using MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetFinancialDashboard;
using MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetGrowthDashboard;
using MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetHealthDashboard;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/dashboard")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class DashboardsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("ceo")]
    public async Task<IActionResult> GetCeo(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCeoDashboardQuery(), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<CeoDashboardDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<CeoDashboardDto>.Ok(result.Value!));
    }

    [HttpGet("financial")]
    public async Task<IActionResult> GetFinancial(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetFinancialDashboardQuery(), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<FinancialDashboardDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<FinancialDashboardDto>.Ok(result.Value!));
    }

    [HttpGet("growth")]
    public async Task<IActionResult> GetGrowth(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetGrowthDashboardQuery(), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<GrowthDashboardDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<GrowthDashboardDto>.Ok(result.Value!));
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetHealthDashboardQuery(), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<HealthDashboardDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<HealthDashboardDto>.Ok(result.Value!));
    }
}
