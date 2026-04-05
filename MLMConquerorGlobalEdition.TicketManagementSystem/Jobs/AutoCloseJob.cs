using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Jobs;

/// <summary>
/// HangFire recurring job — runs every hour.
/// Handles auto-close of resolved tickets and follow-up reminders for inactive tickets.
/// Idempotent: safe to run multiple times.
/// </summary>
public class AutoCloseJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoCloseJob> _logger;

    public AutoCloseJob(IServiceScopeFactory scopeFactory, ILogger<AutoCloseJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        await AutoCloseResolvedTicketsAsync(db, now);
        await SendFollowUpRemindersAsync(db, now);
        await AutoResolveAbandonedTicketsAsync(db, now);

        await db.SaveChangesAsync();
        _logger.LogInformation("AutoCloseJob completed at {Now}", now);
    }

    private async Task AutoCloseResolvedTicketsAsync(AppDbContext db, DateTime now)
    {
        var cutoff = now.AddDays(-7);

        var toClose = await db.Tickets
            .Where(t => !t.IsDeleted
                     && t.Status == TicketStatus.Resolved
                     && t.LastUpdateDate < cutoff)
            .ToListAsync();

        foreach (var ticket in toClose)
        {
            ticket.Status        = TicketStatus.Closed;
            ticket.ClosedAt      = now;
            ticket.LastUpdateDate = now;
            ticket.LastUpdateBy  = "system";

            // Decrement agent count
            if (!string.IsNullOrWhiteSpace(ticket.AssignedToUserId))
            {
                var agent = await db.SupportAgents.FirstOrDefaultAsync(a => a.Id == ticket.AssignedToUserId);
                if (agent is not null && agent.CurrentTicketCount > 0)
                    agent.CurrentTicketCount--;
            }

            db.TicketComments.Add(new TicketComment
            {
                TicketId     = ticket.Id,
                AuthorId     = "system",
                AuthorType   = "system",
                Body         = "Ticket cerrado automáticamente tras 7 días sin actividad desde la resolución.",
                IsInternal   = false,
                CreationDate = now,
                CreatedBy    = "system"
            });

            _logger.LogInformation("Auto-closed ticket {TicketNumber}", ticket.TicketNumber);
        }
    }

    private async Task SendFollowUpRemindersAsync(AppDbContext db, DateTime now)
    {
        var cutoff = now.AddHours(-48);

        var toFollowUp = await db.Tickets
            .Where(t => !t.IsDeleted
                     && t.Status == TicketStatus.WaitingForUser
                     && t.LastUpdateDate < cutoff
                     && !t.FollowUpSent)
            .ToListAsync();

        foreach (var ticket in toFollowUp)
        {
            ticket.FollowUpSent   = true;
            ticket.LastUpdateDate = now;
            ticket.LastUpdateBy   = "system";

            db.TicketComments.Add(new TicketComment
            {
                TicketId     = ticket.Id,
                AuthorId     = "system",
                AuthorType   = "system",
                Body         = "Recordatorio automático enviado al cliente. Por favor confirme si el problema fue resuelto.",
                IsInternal   = true,
                CreationDate = now,
                CreatedBy    = "system"
            });

            _logger.LogInformation("Follow-up sent for ticket {TicketNumber}", ticket.TicketNumber);
        }
    }

    private async Task AutoResolveAbandonedTicketsAsync(AppDbContext db, DateTime now)
    {
        var cutoff = now.AddDays(-7);

        var toResolve = await db.Tickets
            .Where(t => !t.IsDeleted
                     && t.Status == TicketStatus.WaitingForUser
                     && t.LastUpdateDate < cutoff
                     && t.FollowUpSent)
            .ToListAsync();

        foreach (var ticket in toResolve)
        {
            ticket.Status            = TicketStatus.Resolved;
            ticket.ResolvedAt        = now;
            ticket.ResolutionSummary = "Resuelto automáticamente por inactividad del cliente.";
            ticket.LastUpdateDate    = now;
            ticket.LastUpdateBy      = "system";

            db.TicketHistories.Add(new TicketHistory
            {
                TicketId      = ticket.Id,
                Field         = "status",
                OldValue      = TicketStatus.WaitingForUser.ToString(),
                NewValue      = TicketStatus.Resolved.ToString(),
                ChangedByType = "system",
                ChangeReason  = "Auto-resolved due to client inactivity",
                CreationDate  = now,
                CreatedBy     = "system"
            });

            _logger.LogInformation("Auto-resolved ticket {TicketNumber}", ticket.TicketNumber);
        }
    }
}
