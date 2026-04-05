using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.GetTicketSlaStatus;

public class GetTicketSlaStatusHandler : IRequestHandler<GetTicketSlaStatusQuery, Result<SlaStatusDto>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ISlaMonitorService _sla;
    private readonly ICurrentUserService _currentUser;

    public GetTicketSlaStatusHandler(AppDbContext db, IDateTimeProvider dateTime, ISlaMonitorService sla, ICurrentUserService currentUser)
    {
        _db = db;
        _dateTime = dateTime;
        _sla = sla;
        _currentUser = currentUser;
    }

    public async Task<Result<SlaStatusDto>> Handle(GetTicketSlaStatusQuery request, CancellationToken ct)
    {
        var ticket = await _db.Tickets
            .Where(t => t.Id == request.TicketId && !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (ticket is null)
            return Result<SlaStatusDto>.Failure("TICKET_NOT_FOUND", "Ticket not found.");

        if (!_currentUser.IsAdmin && ticket.MemberId != _currentUser.MemberId)
            return Result<SlaStatusDto>.Failure("FORBIDDEN", "Access denied.");

        var now      = _dateTime.Now;
        var remaining = _sla.CalculateRemainingResolutionMinutes(ticket, now);
        var percent   = _sla.CalculatePercentElapsed(ticket, now);

        var color = percent switch
        {
            >= 100 => "red",
            >= 80  => "yellow",
            _      => "green"
        };

        return Result<SlaStatusDto>.Success(new SlaStatusDto
        {
            TicketId                   = ticket.Id,
            SlaPolicyId                = ticket.SlaPolicyId,
            SlaFirstResponseDue        = ticket.SlaFirstResponseDue,
            SlaFirstResponseAt         = ticket.SlaFirstResponseAt,
            SlaResolutionDue           = ticket.SlaResolutionDue,
            IsSlaFirstResponseBreached = ticket.IsSlaFirstResponseBreached,
            IsSlaResolutionBreached    = ticket.IsSlaResolutionBreached,
            IsSlaPaused                = ticket.IsSlaPaused,
            RemainingResolutionMinutes = remaining,
            PercentElapsed             = percent,
            StatusColor                = color
        });
    }
}
