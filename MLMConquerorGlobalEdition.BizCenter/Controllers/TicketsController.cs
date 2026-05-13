using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;
using MLMConquerorGlobalEdition.BizCenter.Features.Tickets.AddTicketComment;
using MLMConquerorGlobalEdition.BizCenter.Features.Tickets.CreateTicket;
using MLMConquerorGlobalEdition.BizCenter.Features.Tickets.GetTicket;
using MLMConquerorGlobalEdition.BizCenter.Features.Tickets.GetTicketCategories;
using MLMConquerorGlobalEdition.BizCenter.Features.Tickets.GetTickets;
using MLMConquerorGlobalEdition.BizCenter.Features.Tickets.UploadAttachment;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

[ApiController]
[Route("api/v1/bizcenter/tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TicketsController(IMediator mediator) => _mediator = mediator;

    /// <summary>GET /api/v1/bizcenter/tickets/categories — list active ticket categories for the create ticket modal.</summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetTicketCategoriesQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<IEnumerable<TicketCategoryDto>>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<IEnumerable<TicketCategoryDto>>.Ok(result.Value!));
    }

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

    /// <summary>POST /api/v1/bizcenter/tickets/{ticketId}/attachments — upload a file attachment for a member's own ticket.</summary>
    [HttpPost("{ticketId}/attachments")]
    [RequestSizeLimit(TicketAttachmentLimits.MaxFileSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = TicketAttachmentLimits.MaxFileSizeBytes)]
    public async Task<IActionResult> UploadAttachment(
        string ticketId,
        [FromForm] IFormFile file,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<TicketAttachmentDto>.Fail("INVALID_FILE", "No file provided."));

        if (file.Length > TicketAttachmentLimits.MaxFileSizeBytes)
            return BadRequest(ApiResponse<TicketAttachmentDto>.Fail("FILE_TOO_LARGE",
                $"Maximum allowed file size is {TicketAttachmentLimits.MaxFileSizeBytes / 1_000_000} MB."));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !TicketAttachmentLimits.AllowedExtensions.Contains(ext))
            return BadRequest(ApiResponse<TicketAttachmentDto>.Fail("INVALID_FILE_TYPE",
                "Allowed types: images, PDF, DOC/DOCX, TXT."));

        // Read the file into memory once. Tickets attachments are capped at 10 MB so
        // buffering is acceptable — keeps the handler signature pure (no IFormFile).
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var result = await _mediator.Send(new UploadAttachmentCommand(
            TicketId:         ticketId,
            OriginalFileName: Path.GetFileName(file.FileName),
            ContentType:      string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType,
            FileSizeBytes:    file.Length,
            Content:          bytes), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<TicketAttachmentDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<TicketAttachmentDto>.Ok(result.Value!));
    }
}

internal static class TicketAttachmentLimits
{
    public const long MaxFileSizeBytes = 10_000_000; // 10 MB

    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp",
        ".pdf",
        ".doc", ".docx",
        ".txt"
    };
}
