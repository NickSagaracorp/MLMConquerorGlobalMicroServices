using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.GetTicket;

public class GetTicketHandler : IRequestHandler<GetTicketQuery, Result<TicketDetailDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetTicketHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<TicketDetailDto>> Handle(GetTicketQuery request, CancellationToken ct)
    {
        var ticket = await _db.Tickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Comments)
            .Include(t => t.Attachments)
            .Where(t => t.Id == request.TicketId && !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (ticket is null)
            return Result<TicketDetailDto>.Failure("TICKET_NOT_FOUND", "Ticket not found.");

        if (!_currentUser.IsAdmin && ticket.MemberId != _currentUser.MemberId)
            return Result<TicketDetailDto>.Failure("FORBIDDEN", "Access denied.");

        var comments = ticket.Comments
            .OrderBy(c => c.CreationDate)
            .Select(c => new TicketCommentDto
            {
                Id = c.Id,
                TicketId = c.TicketId,
                AuthorId = c.AuthorId,
                Content = c.Body,
                IsInternal = c.IsInternal,
                CreationDate = c.CreationDate
            })
            .ToList();

        // Non-admin members do not see internal comments
        if (!_currentUser.IsAdmin)
            comments = comments.Where(c => !c.IsInternal).ToList();

        var attachments = ticket.Attachments
            .OrderBy(a => a.CreationDate)
            .Select(a => new TicketAttachmentDto
            {
                Id = a.Id,
                TicketId = a.TicketId,
                FileName = a.FileName,
                FileUrl = a.FileUrl,
                FileSizeBytes = a.FileSizeBytes,
                ContentType = a.ContentType,
                CreationDate = a.CreationDate
            })
            .ToList();

        var dto = new TicketDetailDto
        {
            Id = ticket.Id,
            Subject = ticket.Subject,
            Body = ticket.Body,
            Status = ticket.Status.ToString(),
            Priority = ticket.Priority.ToString(),
            CategoryId = ticket.CategoryId,
            CategoryName = ticket.Category?.Name,
            MemberId = ticket.MemberId,
            AssignedToUserId = ticket.AssignedToUserId,
            ResolvedAt = ticket.ResolvedAt,
            CreationDate = ticket.CreationDate,
            CommentCount = comments.Count,
            Comments = comments,
            Attachments = attachments
        };

        return Result<TicketDetailDto>.Success(dto);
    }
}
