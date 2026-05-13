using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tickets.GetTicket;

public class GetTicketHandler : IRequestHandler<GetTicketQuery, Result<TicketDetailDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContext;

    public GetTicketHandler(AppDbContext db, ICurrentUserService currentUser, IHttpContextAccessor httpContext)
    {
        _db = db;
        _currentUser = currentUser;
        _httpContext = httpContext;
    }

    public async Task<Result<TicketDetailDto>> Handle(GetTicketQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        var ticket = await _db.Tickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Comments)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId && t.MemberId == memberId && !t.IsDeleted, ct);

        if (ticket is null)
            return Result<TicketDetailDto>.Failure("TICKET_NOT_FOUND", "Ticket not found.");

        // Resolve customer display name from MemberProfile (single lookup; staff names are
        // not resolved here to avoid a join into Identity — staff comments show "Support").
        var memberProfile = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.MemberId == ticket.MemberId)
            .Select(m => new { m.FirstName, m.LastName })
            .FirstOrDefaultAsync(ct);

        var customerDisplayName = memberProfile is not null
            ? $"{memberProfile.FirstName} {memberProfile.LastName}".Trim()
            : ticket.MemberId;

        // Build a server-absolute base for attachment download URLs (Scheme + Host).
        // Attachments are served from /uploads/tickets/{file} via UseStaticFiles().
        var req = _httpContext.HttpContext?.Request;
        var origin = req is not null ? $"{req.Scheme}://{req.Host}" : string.Empty;

        var dto = new TicketDetailDto
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            Subject = ticket.Subject,
            Body = ticket.Body,
            Status = ticket.Status.ToString(),
            Priority = ticket.Priority.ToString(),
            CategoryName = ticket.Category?.Name ?? string.Empty,
            EscalationLevel = (int)ticket.EscalationLevel,
            CreationDate = ticket.CreationDate,
            AssignedToUserId = ticket.AssignedToUserId,
            Comments = ticket.Comments
                .OrderBy(c => c.CreationDate)
                .Select(c =>
                {
                    var isStaff = !string.Equals(c.AuthorType, "customer", StringComparison.OrdinalIgnoreCase);
                    return new TicketCommentDto
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
                .Select(a => new TicketAttachmentDto
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

        return Result<TicketDetailDto>.Success(dto);
    }

    /// <summary>
    /// Returns an absolute download URL. Stored <c>FileUrl</c> may be:
    ///   - already absolute (legacy / external integrations) → returned as-is
    ///   - server-relative (e.g. <c>/uploads/tickets/abc.pdf</c>) → prefixed with the request origin
    /// </summary>
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
