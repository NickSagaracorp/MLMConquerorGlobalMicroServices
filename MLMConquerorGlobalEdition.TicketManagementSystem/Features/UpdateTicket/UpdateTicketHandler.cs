using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.UpdateTicket;

public class UpdateTicketHandler : IRequestHandler<UpdateTicketCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public UpdateTicketHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<bool>> Handle(UpdateTicketCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            return Result<bool>.Failure("FORBIDDEN", "Admin access required.");

        var ticket = await _db.Tickets
            .Where(t => t.Id == request.TicketId && !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (ticket is null)
            return Result<bool>.Failure("TICKET_NOT_FOUND", "Ticket not found.");

        if (request.Request.Status.HasValue)
            ticket.Status = request.Request.Status.Value;

        if (request.Request.Priority.HasValue)
            ticket.Priority = request.Request.Priority.Value;

        if (request.Request.AssignedToUserId is not null)
            ticket.AssignedToUserId = request.Request.AssignedToUserId;

        ticket.LastUpdateDate = _dateTime.Now;
        ticket.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
