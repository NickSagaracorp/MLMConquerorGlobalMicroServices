using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.AddAttachment;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.AddComment;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.AssignTicket;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.CreateTicket;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.GetCategories;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.GetPriorities;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.GetTicket;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.GetTickets;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.MergeTicket;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.ResolveTicket;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.UpdateTicket;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Controllers;

[ApiController]
[Route("api/v1/tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TicketsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // NOTE: Static routes (categories, priorities) are defined BEFORE {ticketId}
    // to prevent routing conflicts.

    /// <summary>GET /api/v1/tickets/categories — list active categories</summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCategoriesQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<IEnumerable<TicketCategoryDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/tickets/priorities — list priority enum values</summary>
    [HttpGet("priorities")]
    public async Task<IActionResult> GetPriorities(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPrioritiesQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<IEnumerable<PriorityDto>>.Ok(result.Value!));
    }

    /// <summary>GET /api/v1/tickets — paged list (members see own; admins see all)</summary>
    [HttpGet]
    public async Task<IActionResult> GetTickets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var pagedRequest = new PagedRequest { Page = page, PageSize = pageSize };
        var result = await _mediator.Send(new GetTicketsQuery(pagedRequest, status), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return Ok(ApiResponse<PagedResult<TicketDto>>.Ok(result.Value!));
    }

    /// <summary>POST /api/v1/tickets — create a new ticket</summary>
    [HttpPost]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateTicketCommand(request), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));

        return CreatedAtAction(nameof(GetTicket), new { ticketId = result.Value!.Id },
            ApiResponse<TicketDto>.Ok(result.Value));
    }

    /// <summary>GET /api/v1/tickets/{ticketId} — get single ticket with comments and attachments</summary>
    [HttpGet("{ticketId}")]
    public async Task<IActionResult> GetTicket(string ticketId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTicketQuery(ticketId), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN")
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));

            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<TicketDetailDto>.Ok(result.Value!));
    }

    /// <summary>PUT /api/v1/tickets/{ticketId} — update ticket (admin only)</summary>
    [HttpPut("{ticketId}")]
    public async Task<IActionResult> UpdateTicket(string ticketId, [FromBody] UpdateTicketRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateTicketCommand(ticketId, request), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN")
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));

            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<bool>.Ok(true));
    }

    /// <summary>POST /api/v1/tickets/{ticketId}/comments — add comment</summary>
    [HttpPost("{ticketId}/comments")]
    public async Task<IActionResult> AddComment(string ticketId, [FromBody] AddCommentRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddCommentCommand(ticketId, request), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN")
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));

            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<TicketCommentDto>.Ok(result.Value!));
    }

    /// <summary>POST /api/v1/tickets/{ticketId}/attachments — add attachment (URL-based)</summary>
    [HttpPost("{ticketId}/attachments")]
    public async Task<IActionResult> AddAttachment(string ticketId, [FromBody] AddAttachmentRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddAttachmentCommand(ticketId, request), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN")
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));

            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<TicketAttachmentDto>.Ok(result.Value!));
    }

    /// <summary>POST /api/v1/tickets/{ticketId}/assign — assign ticket to a user (admin only)</summary>
    [HttpPost("{ticketId}/assign")]
    public async Task<IActionResult> AssignTicket(string ticketId, [FromBody] AssignTicketRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new AssignTicketCommand(ticketId, request), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN")
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));

            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<bool>.Ok(true));
    }

    /// <summary>POST /api/v1/tickets/{ticketId}/merge — merge into another ticket (admin only)</summary>
    [HttpPost("{ticketId}/merge")]
    public async Task<IActionResult> MergeTicket(string ticketId, [FromBody] MergeTicketRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new MergeTicketCommand(ticketId, request), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN")
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));

            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<bool>.Ok(true));
    }

    /// <summary>POST /api/v1/tickets/{ticketId}/resolve — resolve ticket</summary>
    [HttpPost("{ticketId}/resolve")]
    public async Task<IActionResult> ResolveTicket(string ticketId, [FromBody] ResolveTicketRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ResolveTicketCommand(ticketId, request), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN")
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));

            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<bool>.Ok(true));
    }
}
