using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tickets.UploadAttachment;

public class UploadAttachmentHandler : IRequestHandler<UploadAttachmentCommand, Result<TicketAttachmentDto>>
{
    private const string UploadsSubPath = "uploads/tickets";

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpContextAccessor _httpContext;

    public UploadAttachmentHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime,
        IWebHostEnvironment env,
        IHttpContextAccessor httpContext)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
        _env         = env;
        _httpContext = httpContext;
    }

    public async Task<Result<TicketAttachmentDto>> Handle(UploadAttachmentCommand command, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        // Members can only attach to their own tickets. Admins are routed through AdminAPI.
        var ticket = await _db.Tickets
            .FirstOrDefaultAsync(t => t.Id == command.TicketId && t.MemberId == memberId && !t.IsDeleted, ct);

        if (ticket is null)
            return Result<TicketAttachmentDto>.Failure("TICKET_NOT_FOUND", "Ticket not found.");

        var now = _dateTime.UtcNow;

        // Persist to disk under wwwroot/uploads/tickets/{guid}{ext}. The original
        // filename is preserved for display only; the on-disk name is a GUID to
        // prevent path traversal and collisions.
        var ext        = Path.GetExtension(command.OriginalFileName).ToLowerInvariant();
        var diskName   = $"{Guid.NewGuid():N}{ext}";
        var webRoot    = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var folderPath = Path.Combine(webRoot, UploadsSubPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(folderPath);

        var filePath = Path.Combine(folderPath, diskName);
        await File.WriteAllBytesAsync(filePath, command.Content, ct);

        // FileUrl is server-relative — the GetTicket handler turns it into an
        // absolute URL using the request origin. Storing relative keeps the row
        // portable across hosts/ports.
        var fileUrl = $"/{UploadsSubPath}/{diskName}";

        var attachment = new TicketAttachment
        {
            TicketId      = ticket.Id,
            FileName      = command.OriginalFileName,
            FileUrl       = fileUrl,
            FileSizeBytes = command.FileSizeBytes,
            ContentType   = command.ContentType,
            CreatedBy     = _currentUser.UserId,
            CreationDate  = now
        };

        await _db.TicketAttachments.AddAsync(attachment, ct);

        ticket.LastUpdateDate = now;
        ticket.LastUpdateBy   = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        var req    = _httpContext.HttpContext?.Request;
        var origin = req is not null ? $"{req.Scheme}://{req.Host}" : string.Empty;

        var dto = new TicketAttachmentDto
        {
            Id            = attachment.Id,
            FileName      = attachment.FileName,
            FileSizeBytes = attachment.FileSizeBytes,
            ContentType   = attachment.ContentType,
            DownloadUrl   = string.IsNullOrEmpty(origin) ? fileUrl : origin + fileUrl,
            CreationDate  = attachment.CreationDate,
            UploadedBy    = attachment.CreatedBy
        };

        return Result<TicketAttachmentDto>.Success(dto);
    }
}
