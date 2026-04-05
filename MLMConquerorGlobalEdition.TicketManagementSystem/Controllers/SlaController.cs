using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.SlaPolicy;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Controllers;

[ApiController]
[Route("api/v1/helpdesk/sla")]
[Authorize]
public class SlaController : ControllerBase
{
    private readonly IMediator _mediator;

    public SlaController(IMediator mediator) => _mediator = mediator;

    [HttpGet("policies")]
    public async Task<IActionResult> GetPolicies(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSlaPoliciesQuery(), ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        return Ok(ApiResponse<IEnumerable<SlaPolicyDto>>.Ok(result.Value!));
    }

    [HttpPost("policies")]
    public async Task<IActionResult> Create([FromBody] CreateSlaPolicyRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateSlaPolicyCommand(request), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN") return StatusCode(403, ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<SlaPolicyDto>.Ok(result.Value!));
    }

    [HttpPut("policies/{policyId}")]
    public async Task<IActionResult> Update(string policyId, [FromBody] CreateSlaPolicyRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateSlaPolicyCommand(policyId, request), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN") return StatusCode(403, ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpDelete("policies/{policyId}")]
    public async Task<IActionResult> Delete(string policyId, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteSlaPolicyCommand(policyId), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN") return StatusCode(403, ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));
            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<bool>.Ok(true));
    }
}
