using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Jobs;

/// <summary>
/// HangFire recurring job — runs every 5 minutes.
/// Evaluates SLA status for all open tickets and fires breach events.
/// </summary>
public class SlaCheckerJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SlaCheckerJob> _logger;

    public SlaCheckerJob(IServiceScopeFactory scopeFactory, ILogger<SlaCheckerJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sla = scope.ServiceProvider.GetRequiredService<ISlaMonitorService>();

        var now = DateTime.UtcNow;

        var tickets = await db.Tickets
            .Where(t => !t.IsDeleted
                     && t.SlaPolicyId != null
                     && t.Status != TicketStatus.Resolved
                     && t.Status != TicketStatus.Closed
                     && !t.IsSlaPaused)
            .Include(t => t.SlaPolicy)
            .ToListAsync();

        _logger.LogInformation("SlaCheckerJob: evaluating {Count} tickets at {Now}", tickets.Count, now);

        foreach (var ticket in tickets)
        {
            if (ticket.SlaPolicy is null) continue;

            try
            {
                var remaining = sla.CalculateRemainingResolutionMinutes(ticket, now);
                var percent   = sla.CalculatePercentElapsed(ticket, now);

                // First response breach
                if (ticket.SlaFirstResponseAt is null &&
                    ticket.SlaFirstResponseDue.HasValue &&
                    now > ticket.SlaFirstResponseDue.Value &&
                    !ticket.IsSlaFirstResponseBreached)
                {
                    ticket.IsSlaFirstResponseBreached = true;

                    db.SlaBreaches.Add(new SlaBreach
                    {
                        TicketId            = ticket.Id,
                        SlaPolicyId         = ticket.SlaPolicyId!,
                        MetricType          = SlaMetricType.FirstResponse,
                        DueAt               = ticket.SlaFirstResponseDue!.Value,
                        BreachedAt          = now,
                        BreachDurationMinutes = (int)(now - ticket.SlaFirstResponseDue!.Value).TotalMinutes,
                        AssignedAgentId     = ticket.AssignedToUserId,
                        AssignedTeamId      = ticket.AssignedTeamId,
                        CreationDate        = now,
                        CreatedBy           = "system"
                    });

                    _logger.LogWarning("FRT breached: ticket {TicketNumber}", ticket.TicketNumber);
                }

                // Resolution breach
                if (remaining <= 0 && !ticket.IsSlaResolutionBreached)
                {
                    ticket.IsSlaResolutionBreached = true;

                    db.SlaBreaches.Add(new SlaBreach
                    {
                        TicketId            = ticket.Id,
                        SlaPolicyId         = ticket.SlaPolicyId!,
                        MetricType          = SlaMetricType.Resolution,
                        DueAt               = ticket.SlaResolutionDue!.Value,
                        BreachedAt          = now,
                        BreachDurationMinutes = (int)Math.Abs(remaining),
                        AssignedAgentId     = ticket.AssignedToUserId,
                        AssignedTeamId      = ticket.AssignedTeamId,
                        CreationDate        = now,
                        CreatedBy           = "system"
                    });

                    _logger.LogWarning("Resolution SLA breached: ticket {TicketNumber}", ticket.TicketNumber);
                }

                // Auto-escalate if breached past manager threshold
                if (ticket.IsSlaResolutionBreached &&
                    ticket.SlaPolicy.NotifyManagerAtMinutes > 0)
                {
                    var breachDuration = (int)Math.Abs(remaining);
                    if (breachDuration >= ticket.SlaPolicy.NotifyManagerAtMinutes &&
                        ticket.EscalationLevel < EscalationLevel.Tier3)
                    {
                        ticket.EscalationLevel = (EscalationLevel)Math.Min((int)ticket.EscalationLevel + 1, (int)EscalationLevel.Tier3);

                        db.TicketHistories.Add(new TicketHistory
                        {
                            TicketId      = ticket.Id,
                            Field         = "escalationLevel",
                            OldValue      = ((EscalationLevel)((int)ticket.EscalationLevel - 1)).ToString(),
                            NewValue      = ticket.EscalationLevel.ToString(),
                            ChangedByType = "system",
                            ChangeReason  = "Auto-escalated due to SLA breach exceeding manager threshold",
                            CreationDate  = now,
                            CreatedBy     = "system"
                        });
                    }
                }

                ticket.LastUpdateDate = now;
                ticket.LastUpdateBy   = "system";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing SLA for ticket {TicketId}", ticket.Id);
            }
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("SlaCheckerJob: completed");
    }
}
