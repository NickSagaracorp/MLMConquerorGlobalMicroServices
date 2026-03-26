using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;
using MLMConquerorGlobalEdition.BizCenter.Features.Tickets.AddTicketComment;
using MLMConquerorGlobalEdition.BizCenter.Features.Tickets.CreateTicket;
using MLMConquerorGlobalEdition.BizCenter.Features.Tickets.GetTicket;
using MLMConquerorGlobalEdition.BizCenter.Features.Tickets.GetTickets;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

[ApiController]
[Route("api/v1/bizcenter/tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TicketsController(IMediator mediator) => _mediator = mediator;

    /// <summary>GET /api/v1/bizcenter/tickets — paginated tickets for current member</summary>
    [HttpGet]
    public async Task<IActionResult> GetTickets([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetTicketsQuery(page, pageSize), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<TicketDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<PagedResult<TicketDto>>.Ok(result.Value!));
    }

    /// <summary>POST /api/v1/bizcenter/tickets — create a new support ticket</summary>
    [HttpPost]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateTicketCommand(request), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<TicketDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<TicketDto>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/bizcenter/tickets/{ticketId} — get ticket details with comments</summary>
    [HttpGet("{ticketId}")]
    public async Task<IActionResult> GetTicket(string ticketId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTicketQuery(ticketId), ct);
        if (!result.IsSuccess)
            return NotFound(ApiResponse<TicketDetailDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<TicketDetailDto>.Ok(result.Value!));
    }

    /// <summary>POST /api/v1/bizcenter/tickets/{ticketId}/comments — add a comment to a ticket</summary>
    [HttpPost("{ticketId}/comments")]
    public async Task<IActionResult> AddComment(string ticketId, [FromBody] AddCommentRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddTicketCommentCommand(ticketId, request), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<TicketCommentDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<TicketCommentDto>.Ok(result.Value!));
    }
}
