using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporateEvents;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporateEvents.CreateCorporateEvent;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporateEvents.DeleteCorporateEvent;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporateEvents.GetCorporateEvents;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporateEvents.UpdateCorporateEvent;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/corporate-events")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class CorporateEventsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CorporateEventsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetEvents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetCorporateEventsQuery(new PagedRequest { Page = page, PageSize = pageSize }), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<CorporateEventDto>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PagedResult<CorporateEventDto>>.Ok(result.Value!));
    }

    [HttpPost]
    public async Task<IActionResult> CreateEvent(
        [FromBody] CreateCorporateEventRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CreateCorporateEventCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<CorporateEventDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<CorporateEventDto>.Ok(result.Value!));
    }

    [HttpPut("{eventId}")]
    public async Task<IActionResult> UpdateEvent(
        string eventId,
        [FromBody] UpdateCorporateEventRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new UpdateCorporateEventCommand(eventId, request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<CorporateEventDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<CorporateEventDto>.Ok(result.Value!));
    }

    [HttpDelete("{eventId}")]
    public async Task<IActionResult> DeleteEvent(string eventId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DeleteCorporateEventCommand(eventId), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<bool>.Ok(true, "Event deactivated."));
    }
}
