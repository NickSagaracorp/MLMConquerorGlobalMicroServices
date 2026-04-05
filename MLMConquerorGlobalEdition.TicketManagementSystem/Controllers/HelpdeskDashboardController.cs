using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.Dashboard;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Controllers;

[ApiController]
[Route("api/v1/helpdesk/dashboard")]
[Authorize]
public class HelpdeskDashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public HelpdeskDashboardController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetMetrics(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int? teamId,
        [FromQuery] string? agentId,
        [FromQuery] int? categoryId,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetDashboardMetricsQuery(dateFrom, dateTo, teamId, agentId, categoryId), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN") return StatusCode(403, ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<DashboardMetricsDto>.Ok(result.Value!));
    }

    [HttpGet("trends")]
    public async Task<IActionResult> GetTrends([FromQuery] int days = 30, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetDashboardTrendsQuery(days), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN") return StatusCode(403, ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<IEnumerable<DashboardTrendDto>>.Ok(result.Value!));
    }

    [HttpGet("agent-workload")]
    public async Task<IActionResult> GetAgentWorkload([FromQuery] int? teamId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAgentWorkloadQuery(teamId), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN") return StatusCode(403, ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<IEnumerable<AgentWorkloadDto>>.Ok(result.Value!));
    }
}
