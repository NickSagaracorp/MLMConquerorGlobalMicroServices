using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminAddComment;
using MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminAssignTicket;
using MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminResolveTicket;
using MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminUpdateTicket;
using MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminCreateTicket;
using MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminUploadAttachment;
using MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.GetAdminTicketDetail;
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

    [HttpGet("{ticketId}")]
    public async Task<IActionResult> GetTicketDetail(
        string ticketId,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAdminTicketDetailQuery(ticketId), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<AdminTicketDetailDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<AdminTicketDetailDto>.Ok(result.Value!));
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

    [HttpPost("{ticketId}/comments")]
    public async Task<IActionResult> AddComment(
        string ticketId,
        [FromBody] AdminAddCommentRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new AdminAddCommentCommand(ticketId, request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<AdminTicketCommentDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<AdminTicketCommentDto>.Ok(result.Value!));
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

    /// <summary>POST /api/v1/admin/tickets/{ticketId}/attachments — upload a file attachment to any ticket as an admin/staff agent.</summary>
    [HttpPost("{ticketId}/attachments")]
    [RequestSizeLimit(AdminTicketAttachmentLimits.MaxFileSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = AdminTicketAttachmentLimits.MaxFileSizeBytes)]
    public async Task<IActionResult> UploadAttachment(
        string ticketId,
        [FromForm] IFormFile file,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<AdminTicketAttachmentDto>.Fail("INVALID_FILE", "No file provided."));

        if (file.Length > AdminTicketAttachmentLimits.MaxFileSizeBytes)
            return BadRequest(ApiResponse<AdminTicketAttachmentDto>.Fail("FILE_TOO_LARGE",
                $"Maximum allowed file size is {AdminTicketAttachmentLimits.MaxFileSizeBytes / 1_000_000} MB."));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AdminTicketAttachmentLimits.AllowedExtensions.Contains(ext))
            return BadRequest(ApiResponse<AdminTicketAttachmentDto>.Fail("INVALID_FILE_TYPE",
                "Allowed types: images, PDF, DOC/DOCX, TXT."));

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var result = await _mediator.Send(new AdminUploadAttachmentCommand(
            TicketId:         ticketId,
            OriginalFileName: Path.GetFileName(file.FileName),
            ContentType:      string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType,
            FileSizeBytes:    file.Length,
            Content:          bytes), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<AdminTicketAttachmentDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<AdminTicketAttachmentDto>.Ok(result.Value!));
    }
}

internal static class AdminTicketAttachmentLimits
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
