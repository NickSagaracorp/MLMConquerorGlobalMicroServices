using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminAssignTicket;

public class AdminAssignTicketHandler : IRequestHandler<AdminAssignTicketCommand, Result<AdminTicketDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public AdminAssignTicketHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<AdminTicketDto>> Handle(
        AdminAssignTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Category)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
            return Result<AdminTicketDto>.Failure("TICKET_NOT_FOUND", $"Ticket '{request.TicketId}' not found.");

        ticket.AssignedToUserId = request.Request.AssignedToUserId;
        ticket.Status = TicketStatus.InProgress;
        ticket.LastUpdateDate = _dateTime.Now;
        ticket.LastUpdateBy = _currentUser.UserId;

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
