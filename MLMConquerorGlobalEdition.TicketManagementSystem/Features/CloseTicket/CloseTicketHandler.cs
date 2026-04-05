using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.CloseTicket;

public class CloseTicketHandler : IRequestHandler<CloseTicketCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CloseTicketHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<bool>> Handle(CloseTicketCommand request, CancellationToken ct)
    {
        var ticket = await _db.Tickets
            .Where(t => t.Id == request.TicketId && !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (ticket is null)
            return Result<bool>.Failure("TICKET_NOT_FOUND", "Ticket not found.");

        if (ticket.Status == TicketStatus.Closed)
            return Result<bool>.Failure("TICKET_ALREADY_CLOSED", "Ticket is already closed.");

        if (!_currentUser.IsAdmin && ticket.MemberId != _currentUser.MemberId)
            return Result<bool>.Failure("FORBIDDEN", "Access denied.");

        var now = _dateTime.Now;
        ticket.Status        = TicketStatus.Closed;
        ticket.ClosedAt      = now;
        ticket.LastUpdateDate = now;
        ticket.LastUpdateBy  = _currentUser.UserId;

        // Decrement agent ticket count
        if (!string.IsNullOrWhiteSpace(ticket.AssignedToUserId))
        {
            var agent = await _db.SupportAgents
                .FirstOrDefaultAsync(a => a.Id == ticket.AssignedToUserId, ct);
            if (agent is not null && agent.CurrentTicketCount > 0)
                agent.CurrentTicketCount--;
        }

        _db.TicketHistories.Add(new TicketHistory
        {
            TicketId      = ticket.Id,
            Field         = "status",
            OldValue      = ticket.Status.ToString(),
            NewValue      = TicketStatus.Closed.ToString(),
            ChangedByType = _currentUser.IsAdmin ? "agent" : "customer",
            ChangedById   = _currentUser.UserId,
            CreationDate  = now,
            CreatedBy     = _currentUser.UserId
        });

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
