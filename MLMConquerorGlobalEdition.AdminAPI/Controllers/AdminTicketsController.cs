using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminAssignTicket;
using MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminResolveTicket;
using MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminUpdateTicket;
using MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminCreateTicket;
using MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.GetAdminTickets;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/tickets")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminTicketsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminTicketsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetTickets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetAdminTicketsQuery(new PagedRequest { Page = page, PageSize = pageSize }), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<AdminTicketDto>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PagedResult<AdminTicketDto>>.Ok(result.Value!));
    }

    [HttpPut("{ticketId}")]
    public async Task<IActionResult> UpdateTicket(
        string ticketId,
        [FromBody] AdminUpdateTicketRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new AdminUpdateTicketCommand(ticketId, request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<AdminTicketDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<AdminTicketDto>.Ok(result.Value!));
    }

    [HttpPost("{ticketId}/assign")]
    public async Task<IActionResult> AssignTicket(
        string ticketId,
        [FromBody] AdminAssignTicketRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new AdminAssignTicketCommand(ticketId, request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<AdminTicketDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<AdminTicketDto>.Ok(result.Value!));
    }

    [HttpPost("{ticketId}/resolve")]
    public async Task<IActionResult> ResolveTicket(
        string ticketId,
        [FromBody] AdminResolveTicketRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new AdminResolveTicketCommand(ticketId, request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<AdminTicketDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<AdminTicketDto>.Ok(result.Value!));
    }

    [HttpPost]
    public async Task<IActionResult> CreateTicket(
        [FromBody] AdminCreateTicketRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new AdminCreateTicketCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<AdminTicketDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<AdminTicketDto>.Ok(result.Value!));
    }
}
