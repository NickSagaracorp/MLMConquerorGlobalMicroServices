using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.SupportAdmin;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Controllers;

[ApiController]
[Route("api/v1/helpdesk/admin")]
[Authorize]
public class SupportAdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public SupportAdminController(IMediator mediator) => _mediator = mediator;


    [HttpGet("teams")]
    public async Task<IActionResult> GetTeams(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSupportTeamsQuery(), ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        return Ok(ApiResponse<IEnumerable<SupportTeamDto>>.Ok(result.Value!));
    }

    [HttpPost("teams")]
    public async Task<IActionResult> CreateTeam([FromBody] CreateSupportTeamRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateSupportTeamCommand(request), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN") return StatusCode(403, ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<SupportTeamDto>.Ok(result.Value!));
    }

    [HttpPut("teams/{teamId:int}")]
    public async Task<IActionResult> UpdateTeam(int teamId, [FromBody] CreateSupportTeamRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateSupportTeamCommand(teamId, request), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN") return StatusCode(403, ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));
            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<bool>.Ok(true));
    }


    [HttpGet("agents")]
    public async Task<IActionResult> GetAgents([FromQuery] int? teamId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetSupportAgentsQuery(teamId), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN") return StatusCode(403, ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<IEnumerable<AgentWorkloadDto>>.Ok(result.Value!));
    }

    [HttpPost("agents")]
    public async Task<IActionResult> CreateAgent([FromBody] CreateSupportAgentRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateSupportAgentCommand(request), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN") return StatusCode(403, ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<AgentWorkloadDto>.Ok(result.Value!));
    }

    [HttpPatch("agents/{agentId}/availability")]
    public async Task<IActionResult> UpdateAvailability(string agentId, [FromBody] UpdateAvailabilityRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateAgentAvailabilityCommand(agentId, request.Availability), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN") return StatusCode(403, ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<bool>.Ok(true));
    }
}

public class UpdateAvailabilityRequest
{
    public string Availability { get; set; } = "available";
}
