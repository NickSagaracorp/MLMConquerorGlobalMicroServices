using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.GetAdminTicketDetail;

public class GetAdminTicketDetailHandler : IRequestHandler<GetAdminTicketDetailQuery, Result<AdminTicketDetailDto>>
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContext;

    public GetAdminTicketDetailHandler(AppDbContext db, IHttpContextAccessor httpContext)
    {
        _db = db;
        _httpContext = httpContext;
    }

    public async Task<Result<AdminTicketDetailDto>> Handle(
        GetAdminTicketDetailQuery request, CancellationToken ct)
    {
        var ticket = await _db.Tickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Comments)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId && !t.IsDeleted, ct);

        if (ticket is null)
            return Result<AdminTicketDetailDto>.Failure("TICKET_NOT_FOUND", $"Ticket '{request.TicketId}' not found.");

        // Resolve customer display name from MemberProfile
        var memberProfile = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.MemberId == ticket.MemberId)
            .Select(m => new { m.FirstName, m.LastName })
            .FirstOrDefaultAsync(ct);

        var customerDisplayName = memberProfile is not null
            ? $"{memberProfile.FirstName} {memberProfile.LastName}".Trim()
            : ticket.MemberId;

        // For relative FileUrl values stored by the BizCenter or AdminAPI upload
        // endpoints, the download URL is rebuilt against the *current* request
        // origin (so the BizCenterWeb shell can hit AdminAPI's static-files mount).
        var req    = _httpContext.HttpContext?.Request;
        var origin = req is not null ? $"{req.Scheme}://{req.Host}" : string.Empty;

        var dto = new AdminTicketDetailDto
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            Subject = ticket.Subject,
            Body = ticket.Body,
            MemberId = ticket.MemberId,
            Status = ticket.Status.ToString(),
            Priority = ticket.Priority.ToString(),
            CategoryName = ticket.Category?.Name,
            AssignedToUserId = ticket.AssignedToUserId,
            EscalationLevel = (int)ticket.EscalationLevel,
            CreationDate = ticket.CreationDate,
            CommentCount = ticket.Comments.Count,
            Comments = ticket.Comments
                .OrderBy(c => c.CreationDate)
                .Select(c =>
                {
                    var isStaff = !string.Equals(c.AuthorType, "customer", StringComparison.OrdinalIgnoreCase);
                    return new AdminTicketCommentDto
                    {
                        Id = c.Id,
                        AuthorId = c.AuthorId,
                        Author = isStaff ? "Support" : customerDisplayName,
                        Body = c.Body,
                        IsStaff = isStaff,
                        CreationDate = c.CreationDate
                    };
                })
                .ToList(),
            Attachments = ticket.Attachments
                .OrderBy(a => a.CreationDate)
                .Select(a => new AdminTicketAttachmentDto
                {
                    Id            = a.Id,
                    FileName      = a.FileName,
                    FileSizeBytes = a.FileSizeBytes,
                    ContentType   = a.ContentType,
                    DownloadUrl   = BuildDownloadUrl(origin, a.FileUrl),
                    CreationDate  = a.CreationDate,
                    UploadedBy    = a.CreatedBy
                })
                .ToList()
        };

        return Result<AdminTicketDetailDto>.Success(dto);
    }

    private static string BuildDownloadUrl(string origin, string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)) return string.Empty;
        if (fileUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            fileUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return fileUrl;
        if (string.IsNullOrEmpty(origin)) return fileUrl;
        return fileUrl.StartsWith('/') ? origin + fileUrl : $"{origin}/{fileUrl}";
    }
}
