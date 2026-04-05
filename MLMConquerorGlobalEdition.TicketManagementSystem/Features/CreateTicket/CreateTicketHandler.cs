using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.CreateTicket;

public class CreateTicketHandler : IRequestHandler<CreateTicketCommand, Result<TicketDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly IRoutingEngine _routing;
    private readonly ISlaMonitorService _sla;

    public CreateTicketHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime,
        IRoutingEngine routing,
        ISlaMonitorService sla)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _routing = routing;
        _sla = sla;
    }

    public async Task<Result<TicketDto>> Handle(CreateTicketCommand request, CancellationToken ct)
    {
        var now = _dateTime.Now;

        // Generate HD-YYYYMMDD-NNNN ticket number atomically
        var ticketNumber = await GenerateTicketNumberAsync(now, ct);

        var ticket = new Ticket
        {
            TicketNumber    = ticketNumber,
            MemberId        = _currentUser.MemberId,
            Subject         = request.Request.Subject,
            Body            = request.Request.Body,
            CategoryId      = request.Request.CategoryId,
            Priority        = request.Request.Priority,
            Channel         = request.Request.Channel,
            Status          = TicketStatus.Open,
            Language        = request.Request.Language,
            CreationDate    = now,
            CreatedBy       = _currentUser.UserId,
            LastUpdateDate  = now,
            LastUpdateBy    = _currentUser.UserId
        };

        // Assign SLA policy
        var category = await _db.TicketCategories
            .FirstOrDefaultAsync(c => c.Id == ticket.CategoryId, ct);

        var slaPolicyId = category?.DefaultSlaPolicyId;
        if (slaPolicyId is null)
        {
            var defaultPolicy = await _db.SlaPolicies
                .Where(p => p.IsDefault && p.IsActive && !p.IsDeleted)
                .FirstOrDefaultAsync(ct);
            slaPolicyId = defaultPolicy?.Id;
        }

        if (slaPolicyId is not null)
        {
            var policy = await _db.SlaPolicies
                .FirstOrDefaultAsync(p => p.Id == slaPolicyId && !p.IsDeleted, ct);

            if (policy is not null)
            {
                ticket.SlaPolicyId = policy.Id;
                _sla.AssignSlaDeadlines(ticket, policy, now);
            }
        }

        await _db.Tickets.AddAsync(ticket, ct);
        await _db.SaveChangesAsync(ct);

        // Auto-routing (after save so ticket.Id is available)
        var routingResult = await _routing.RouteAsync(ticket, ct);

        if (routingResult.AgentId is not null || routingResult.TeamId is not null)
        {
            ticket.AssignedToUserId = routingResult.AgentId;
            ticket.AssignedTeamId  = routingResult.TeamId;

            if (routingResult.AgentId is not null)
            {
                ticket.Status = TicketStatus.InProgress;

                // Increment agent's ticket count
                var agent = await _db.SupportAgents
                    .FirstOrDefaultAsync(a => a.Id == routingResult.AgentId && !a.IsDeleted, ct);
                if (agent is not null)
                    agent.CurrentTicketCount++;
            }

            // Log history
            _db.TicketHistories.Add(new TicketHistory
            {
                TicketId      = ticket.Id,
                Field         = "assignedTo",
                OldValue      = null,
                NewValue      = routingResult.AgentId ?? $"team:{routingResult.TeamId}",
                ChangedByType = "system",
                CreationDate  = now,
                CreatedBy     = "system"
            });

            await _db.SaveChangesAsync(ct);
        }

        var dto = MapToDto(ticket, category?.Name);
        return Result<TicketDto>.Success(dto);
    }

    private async Task<string> GenerateTicketNumberAsync(DateTime utcNow, CancellationToken ct)
    {
        var today = utcNow.Date;

        var seq = await _db.TicketSequences.FindAsync([today], ct);
        if (seq is null)
        {
            seq = new TicketSequence { Date = today, LastSequence = 1 };
            _db.TicketSequences.Add(seq);
        }
        else
        {
            seq.LastSequence++;
        }

        await _db.SaveChangesAsync(ct);
        return $"HD-{today:yyyyMMdd}-{seq.LastSequence:D4}";
    }

    private static TicketDto MapToDto(Ticket ticket, string? categoryName) => new()
    {
        Id              = ticket.Id,
        TicketNumber    = ticket.TicketNumber,
        Subject         = ticket.Subject,
        Body            = ticket.Body,
        Status          = ticket.Status.ToString(),
        Priority        = ticket.Priority.ToString(),
        Channel         = ticket.Channel.ToString(),
        EscalationLevel = ticket.EscalationLevel.ToString(),
        CategoryId      = ticket.CategoryId,
        CategoryName    = categoryName,
        MemberId        = ticket.MemberId,
        AssignedToUserId = ticket.AssignedToUserId,
        AssignedTeamId  = ticket.AssignedTeamId,
        SlaPolicyId     = ticket.SlaPolicyId,
        SlaResolutionDue = ticket.SlaResolutionDue,
        IsSlaBreached   = ticket.IsSlaResolutionBreached,
        ResolvedAt      = ticket.ResolvedAt,
        CreationDate    = ticket.CreationDate,
        CommentCount    = 0
    };
}
