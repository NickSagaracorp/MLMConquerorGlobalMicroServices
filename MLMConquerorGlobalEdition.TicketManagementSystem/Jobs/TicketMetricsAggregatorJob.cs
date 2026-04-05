using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Jobs;

/// <summary>
/// HangFire recurring job — runs nightly at 1:00 AM UTC.
/// Aggregates daily metrics into TicketMetricsDaily (UPSERT by date — idempotent).
/// </summary>
public class TicketMetricsAggregatorJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TicketMetricsAggregatorJob> _logger;

    public TicketMetricsAggregatorJob(IServiceScopeFactory scopeFactory, ILogger<TicketMetricsAggregatorJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var targetDate = DateTime.UtcNow.Date.AddDays(-1);

        _logger.LogInformation("TicketMetricsAggregatorJob: aggregating for {Date}", targetDate);

        var from = targetDate;
        var to   = targetDate.AddDays(1).AddTicks(-1);

        var ticketsCreated = await db.Tickets
            .Where(t => !t.IsDeleted && t.CreationDate >= from && t.CreationDate <= to)
            .ToListAsync();

        var ticketsResolved = await db.Tickets
            .Where(t => !t.IsDeleted && t.ResolvedAt >= from && t.ResolvedAt <= to)
            .ToListAsync();

        var ticketsClosed = await db.Tickets
            .Where(t => !t.IsDeleted && t.ClosedAt >= from && t.ClosedAt <= to)
            .ToListAsync();

        // FRT: from creation to first response
        var frtSamples = ticketsCreated
            .Where(t => t.SlaFirstResponseAt.HasValue)
            .Select(t => (t.SlaFirstResponseAt!.Value - t.CreationDate).TotalMinutes)
            .ToList();

        // MTTR: from creation to resolution
        var mttrSamples = ticketsResolved
            .Where(t => t.ResolvedAt.HasValue)
            .Select(t => (t.ResolvedAt!.Value - t.CreationDate).TotalMinutes)
            .ToList();

        // FCR: resolved without escalation
        var fcrCount    = ticketsResolved.Count(t => t.EscalationLevel == EscalationLevel.Tier1);
        var fcrRate     = ticketsResolved.Count > 0 ? (double)fcrCount / ticketsResolved.Count * 100 : 0;

        // CSAT
        var csatSamples = ticketsClosed.Where(t => t.CsatScore.HasValue).ToList();
        var csatAvg     = csatSamples.Count > 0 ? csatSamples.Average(t => t.CsatScore!.Value) : 0;

        // SLA compliance
        var totalWithSla   = ticketsResolved.Count(t => t.SlaPolicyId != null);
        var compliantCount = ticketsResolved.Count(t => t.SlaPolicyId != null && !t.IsSlaResolutionBreached);
        var slaRate        = totalWithSla > 0 ? (double)compliantCount / totalWithSla * 100 : 100;

        // Breach counts
        var breaches  = await db.SlaBreaches.Where(b => b.BreachedAt >= from && b.BreachedAt <= to).ToListAsync();
        var frtBreaches = breaches.Count(b => b.MetricType == SlaMetricType.FirstResponse);
        var resBreaches = breaches.Count(b => b.MetricType == SlaMetricType.Resolution);

        // Distributions
        var byPriority = ticketsCreated.GroupBy(t => t.Priority.ToString()).ToDictionary(g => g.Key, g => g.Count());
        var byCategory = ticketsCreated.GroupBy(t => t.CategoryId.ToString()).ToDictionary(g => g.Key, g => g.Count());
        var byChannel  = ticketsCreated.GroupBy(t => t.Channel.ToString()).ToDictionary(g => g.Key, g => g.Count());
        var byAgent    = ticketsCreated.Where(t => t.AssignedToUserId != null)
            .GroupBy(t => t.AssignedToUserId!).ToDictionary(g => g.Key, g => g.Count());

        // UPSERT
        var existing = await db.TicketMetrics.FirstOrDefaultAsync(m => m.Date == targetDate);

        if (existing is null)
        {
            existing = new TicketMetricsDaily
            {
                Date         = targetDate,
                CreationDate = DateTime.UtcNow,
                CreatedBy    = "system",
                LastUpdateDate = DateTime.UtcNow,
                LastUpdateBy  = "system"
            };
            await db.TicketMetrics.AddAsync(existing);
        }

        existing.TotalCreated               = ticketsCreated.Count;
        existing.TotalResolved              = ticketsResolved.Count;
        existing.TotalClosed                = ticketsClosed.Count;
        existing.AvgFirstResponseMinutes    = frtSamples.Count > 0 ? Math.Round(frtSamples.Average(), 1) : 0;
        existing.AvgResolutionMinutes       = mttrSamples.Count > 0 ? Math.Round(mttrSamples.Average(), 1) : 0;
        existing.FirstContactResolutionRate = Math.Round(fcrRate, 1);
        existing.SlaComplianceRate          = Math.Round(slaRate, 1);
        existing.CsatAverage                = Math.Round(csatAvg, 2);
        existing.CsatResponseCount          = csatSamples.Count;
        existing.FrtBreaches                = frtBreaches;
        existing.ResolutionBreaches         = resBreaches;
        existing.TicketsByPriorityJson      = System.Text.Json.JsonSerializer.Serialize(byPriority);
        existing.TicketsByCategoryJson      = System.Text.Json.JsonSerializer.Serialize(byCategory);
        existing.TicketsByChannelJson       = System.Text.Json.JsonSerializer.Serialize(byChannel);
        existing.TicketsByAgentJson         = System.Text.Json.JsonSerializer.Serialize(byAgent);
        existing.LastUpdateDate             = DateTime.UtcNow;
        existing.LastUpdateBy               = "system";

        await db.SaveChangesAsync();
        _logger.LogInformation("TicketMetricsAggregatorJob: completed for {Date}", targetDate);
    }
}
