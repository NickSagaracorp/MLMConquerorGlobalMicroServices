using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.EscalateTicket;

public class EscalateTicketHandler : IRequestHandler<EscalateTicketCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public EscalateTicketHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<bool>> Handle(EscalateTicketCommand request, CancellationToken ct)
    {
        var ticket = await _db.Tickets
            .Where(t => t.Id == request.TicketId && !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (ticket is null)
            return Result<bool>.Failure("TICKET_NOT_FOUND", "Ticket not found.");

        if (!_currentUser.IsAdmin && !_currentUser.Roles.Contains("Agent"))
            return Result<bool>.Failure("FORBIDDEN", "Only agents or admins can escalate tickets.");

        if (ticket.Status == TicketStatus.Resolved || ticket.Status == TicketStatus.Closed)
            return Result<bool>.Failure("TICKET_ALREADY_CLOSED", "Cannot escalate a resolved or closed ticket.");

        var now = _dateTime.Now;
        var oldLevel = ticket.EscalationLevel;
        var newLevel = (EscalationLevel)Math.Min((int)ticket.EscalationLevel + 1, (int)EscalationLevel.Tier3);

        ticket.EscalationLevel = newLevel;
        ticket.Status          = TicketStatus.InProgress;
        ticket.LastUpdateDate  = now;
        ticket.LastUpdateBy    = _currentUser.UserId;

        // Try to assign to a Tier 2 agent in the team
        if (ticket.AssignedTeamId.HasValue)
        {
            var tier2Agent = await _db.SupportAgents
                .Where(a => a.TeamId == ticket.AssignedTeamId
                         && a.Tier >= 2
                         && a.IsActive
                         && !a.IsDeleted
                         && a.Availability != "offline"
                         && a.CurrentTicketCount < a.MaxConcurrentTickets)
                .OrderBy(a => a.CurrentTicketCount)
                .FirstOrDefaultAsync(ct);

            if (tier2Agent is not null)
            {
                // Decrement previous agent count
                if (!string.IsNullOrWhiteSpace(ticket.AssignedToUserId))
                {
                    var prevAgent = await _db.SupportAgents
                        .FirstOrDefaultAsync(a => a.Id == ticket.AssignedToUserId, ct);
                    if (prevAgent is not null && prevAgent.CurrentTicketCount > 0)
                        prevAgent.CurrentTicketCount--;
                }

                ticket.AssignedToUserId = tier2Agent.Id;
                tier2Agent.CurrentTicketCount++;
            }
            else
            {
                // Fallback to team supervisor
                var team = await _db.SupportTeams.FindAsync([ticket.AssignedTeamId.Value], ct);
                if (team?.SupervisorAgentId is not null)
                    ticket.AssignedToUserId = team.SupervisorAgentId;
            }
        }

        // Internal system comment with escalation reason
        _db.TicketComments.Add(new TicketComment
        {
            TicketId     = ticket.Id,
            AuthorId     = _currentUser.UserId,
            AuthorType   = "system",
            Body         = $"Ticket escalado a {newLevel}. Motivo: {request.Request.Reason}",
            IsInternal   = true,
            CreationDate = now,
            CreatedBy    = _currentUser.UserId
        });

        // History record
        _db.TicketHistories.Add(new TicketHistory
        {
            TicketId      = ticket.Id,
            Field         = "escalationLevel",
            OldValue      = oldLevel.ToString(),
            NewValue      = newLevel.ToString(),
            ChangedByType = "agent",
            ChangedById   = _currentUser.UserId,
            ChangeReason  = request.Request.Reason,
            CreationDate  = now,
            CreatedBy     = _currentUser.UserId
        });

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
