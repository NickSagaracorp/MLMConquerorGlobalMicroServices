using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.CannedResponses;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Controllers;

[ApiController]
[Route("api/v1/helpdesk/canned-responses")]
[Authorize]
public class CannedResponsesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CannedResponsesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCannedResponsesQuery(), ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        return Ok(ApiResponse<IEnumerable<CannedResponseDto>>.Ok(result.Value!));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCannedResponseRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateCannedResponseCommand(request), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN") return StatusCode(403, ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<CannedResponseDto>.Ok(result.Value!));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] CreateCannedResponseRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateCannedResponseCommand(id, request), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN") return StatusCode(403, ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));
            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteCannedResponseCommand(id), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN") return StatusCode(403, ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));
            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] ApplyCannedResponseRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ApplyCannedResponseCommand(request), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        return Ok(ApiResponse<string>.Ok(result.Value!));
    }
}
