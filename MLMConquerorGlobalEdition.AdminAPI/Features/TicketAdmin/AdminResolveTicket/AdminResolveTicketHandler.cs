using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminResolveTicket;

public class AdminResolveTicketHandler : IRequestHandler<AdminResolveTicketCommand, Result<AdminTicketDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public AdminResolveTicketHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<AdminTicketDto>> Handle(
        AdminResolveTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Category)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
            return Result<AdminTicketDto>.Failure("TICKET_NOT_FOUND", $"Ticket '{request.TicketId}' not found.");

        var now = _dateTime.Now;
        ticket.Status = TicketStatus.Resolved;
        ticket.ResolvedAt = now;
        ticket.LastUpdateDate = now;
        ticket.LastUpdateBy = _currentUser.UserId;

        if (!string.IsNullOrWhiteSpace(request.Request.ResolutionNotes))
        {
            var comment = new TicketComment
            {
                TicketId = ticket.Id,
                AuthorId = _currentUser.UserId,
                Body = request.Request.ResolutionNotes,
                IsInternal = true,
                CreationDate = now,
                CreatedBy = _currentUser.UserId
            };
            await _db.TicketComments.AddAsync(comment, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var dto = new AdminTicketDto
        {
            Id = ticket.Id,
            Subject = ticket.Subject,
            MemberId = ticket.MemberId,
            Status = ticket.Status.ToString(),
            Priority = ticket.Priority.ToString(),
            CategoryName = ticket.Category?.Name,
            AssignedToUserId = ticket.AssignedToUserId,
            CreationDate = ticket.CreationDate,
            CommentCount = ticket.Comments.Count
        };

        return Result<AdminTicketDto>.Success(dto);
    }
}
