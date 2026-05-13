using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminUploadAttachment;

public class AdminUploadAttachmentHandler
    : IRequestHandler<AdminUploadAttachmentCommand, Result<AdminTicketAttachmentDto>>
{
    private const string UploadsSubPath = "uploads/tickets";

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpContextAccessor _httpContext;

    public AdminUploadAttachmentHandler(
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

    public async Task<Result<AdminTicketAttachmentDto>> Handle(
        AdminUploadAttachmentCommand command, CancellationToken ct)
    {
        var ticket = await _db.Tickets
            .FirstOrDefaultAsync(t => t.Id == command.TicketId && !t.IsDeleted, ct);

        if (ticket is null)
            return Result<AdminTicketAttachmentDto>.Failure("TICKET_NOT_FOUND", "Ticket not found.");

        var now = _dateTime.Now;

        var ext        = Path.GetExtension(command.OriginalFileName).ToLowerInvariant();
        var diskName   = $"{Guid.NewGuid():N}{ext}";
        var webRoot    = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var folderPath = Path.Combine(webRoot, UploadsSubPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(folderPath);

        var filePath = Path.Combine(folderPath, diskName);
        await File.WriteAllBytesAsync(filePath, command.Content, ct);

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

        var dto = new AdminTicketAttachmentDto
        {
            Id            = attachment.Id,
            FileName      = attachment.FileName,
            FileSizeBytes = attachment.FileSizeBytes,
            ContentType   = attachment.ContentType,
            DownloadUrl   = string.IsNullOrEmpty(origin) ? fileUrl : origin + fileUrl,
            CreationDate  = attachment.CreationDate,
            UploadedBy    = attachment.CreatedBy
        };

        return Result<AdminTicketAttachmentDto>.Success(dto);
    }
}
