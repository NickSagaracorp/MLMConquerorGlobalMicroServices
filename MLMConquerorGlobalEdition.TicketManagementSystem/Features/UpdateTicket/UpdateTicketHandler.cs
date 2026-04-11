using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.UpdateTicket;

public class UpdateTicketHandler : IRequestHandler<UpdateTicketCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly IPushNotificationService _push;

    public UpdateTicketHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime, IPushNotificationService push)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _push = push;
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

        var statusChanged = request.Request.Status.HasValue && request.Request.Status.Value != ticket.Status;

        if (request.Request.Status.HasValue)
            ticket.Status = request.Request.Status.Value;

        if (request.Request.Priority.HasValue)
            ticket.Priority = request.Request.Priority.Value;

        if (request.Request.AssignedToUserId is not null)
            ticket.AssignedToUserId = request.Request.AssignedToUserId;

        ticket.LastUpdateDate = _dateTime.Now;
        ticket.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        if (statusChanged && !string.IsNullOrEmpty(ticket.MemberId))
        {
            _ = _push.SendAsync(
                ticket.MemberId,
                NotificationEvents.TicketStatusChanged,
                "Ticket Updated",
                $"Your ticket #{ticket.Id[..8]} status changed to {ticket.Status}.",
                ct);
        }

        return Result<bool>.Success(true);
    }
}
