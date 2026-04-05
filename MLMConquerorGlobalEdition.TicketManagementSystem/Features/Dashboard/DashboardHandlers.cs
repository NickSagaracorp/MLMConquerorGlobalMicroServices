using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Features.Dashboard;

public record GetDashboardMetricsQuery(
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    int? TeamId = null,
    string? AgentId = null,
    int? CategoryId = null) : IRequest<Result<DashboardMetricsDto>>;

public record GetDashboardTrendsQuery(int Days = 30) : IRequest<Result<IEnumerable<DashboardTrendDto>>>;
public record GetAgentWorkloadQuery(int? TeamId = null) : IRequest<Result<IEnumerable<AgentWorkloadDto>>>;

public class GetDashboardMetricsHandler : IRequestHandler<GetDashboardMetricsQuery, Result<DashboardMetricsDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly ISlaMonitorService _sla;

    public GetDashboardMetricsHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime, ISlaMonitorService sla)
        => (_db, _currentUser, _dateTime, _sla) = (db, currentUser, dateTime, sla);

    public async Task<Result<DashboardMetricsDto>> Handle(GetDashboardMetricsQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin && !_currentUser.Roles.Contains("Agent") && !_currentUser.Roles.Contains("Supervisor"))
            return Result<DashboardMetricsDto>.Failure("FORBIDDEN", "Access denied.");

        var now  = _dateTime.Now;
        var from = request.DateFrom ?? now.AddDays(-30);
        var to   = request.DateTo   ?? now;

        // Real-time ticket counts
        var allOpenTickets = await _db.Tickets
            .Where(t => !t.IsDeleted
                && t.Status != TicketStatus.Closed
                && (!request.TeamId.HasValue || t.AssignedTeamId == request.TeamId)
                && (!request.CategoryId.HasValue || t.CategoryId == request.CategoryId))
            .ToListAsync(ct);

        var openCount           = allOpenTickets.Count(t => t.Status == TicketStatus.Open);
        var inProgressCount     = allOpenTickets.Count(t => t.Status == TicketStatus.InProgress);
        var waitingCustomerCount = allOpenTickets.Count(t => t.Status == TicketStatus.WaitingForUser);
        var escalatedCount      = allOpenTickets.Count(t => t.EscalationLevel > EscalationLevel.Tier1);
        var resolvedCount       = allOpenTickets.Count(t => t.Status == TicketStatus.Resolved);

        // Historical metrics from daily snapshots
        var metrics = await _db.TicketMetrics
            .Where(m => m.Date >= from.Date && m.Date <= to.Date)
            .OrderBy(m => m.Date)
            .ToListAsync(ct);

        var avgFrt  = metrics.Any() ? metrics.Average(m => m.AvgFirstResponseMinutes) : 0;
        var avgMttr = metrics.Any() ? metrics.Average(m => m.AvgResolutionMinutes) : 0;
        var avgFcr  = metrics.Any() ? metrics.Average(m => m.FirstContactResolutionRate) : 0;
        var avgCsat = metrics.Any(m => m.CsatResponseCount > 0) ? metrics.Where(m => m.CsatResponseCount > 0).Average(m => m.CsatAverage) : 0;
        var slaComp = metrics.Any() ? metrics.Average(m => m.SlaComplianceRate) : 0;
        var frtBre  = metrics.Sum(m => m.FrtBreaches);
        var resBre  = metrics.Sum(m => m.ResolutionBreaches);

        // Agent workload
        var agentsQuery = _db.SupportAgents.Where(a => a.IsActive && !a.IsDeleted);
        if (request.TeamId.HasValue)
            agentsQuery = agentsQuery.Where(a => a.TeamId == request.TeamId);

        var agents = await agentsQuery.ToListAsync(ct);

        var agentWorkloads = agents.Select(a => new AgentWorkloadDto
        {
            AgentId             = a.Id,
            DisplayName         = a.DisplayName,
            Email               = a.Email,
            Tier                = a.Tier,
            Availability        = a.Availability,
            CurrentTicketCount  = a.CurrentTicketCount,
            MaxConcurrentTickets = a.MaxConcurrentTickets,
            WorkloadPercent     = a.MaxConcurrentTickets > 0 ? Math.Round((double)a.CurrentTicketCount / a.MaxConcurrentTickets * 100, 1) : 0
        }).ToList();

        // Tickets by channel and category (from period)
        var periodTickets = await _db.Tickets
            .Where(t => !t.IsDeleted && t.CreationDate >= from && t.CreationDate <= to
                && (!request.TeamId.HasValue || t.AssignedTeamId == request.TeamId)
                && (!request.CategoryId.HasValue || t.CategoryId == request.CategoryId))
            .ToListAsync(ct);

        var byChannel  = periodTickets.GroupBy(t => t.Channel.ToString()).ToDictionary(g => g.Key, g => g.Count());
        var byCategory = periodTickets.GroupBy(t => t.CategoryId.ToString()).ToDictionary(g => g.Key, g => g.Count());

        return Result<DashboardMetricsDto>.Success(new DashboardMetricsDto
        {
            FrtMinutes            = Math.Round(avgFrt, 1),
            MttrMinutes           = Math.Round(avgMttr, 1),
            FcrPercent            = Math.Round(avgFcr, 1),
            CsatAverage           = Math.Round(avgCsat, 2),
            OpenCount             = openCount,
            InProgressCount       = inProgressCount,
            WaitingCustomerCount  = waitingCustomerCount,
            EscalatedCount        = escalatedCount,
            ResolvedCount         = resolvedCount,
            SlaComplianceRate     = Math.Round(slaComp, 1),
            FrtBreachesToday      = frtBre,
            ResolutionBreachesToday = resBre,
            TicketsByChannel      = byChannel,
            TicketsByCategory     = byCategory,
            AgentWorkloads        = agentWorkloads,
            GeneratedAt           = now
        });
    }
}

public class GetDashboardTrendsHandler : IRequestHandler<GetDashboardTrendsQuery, Result<IEnumerable<DashboardTrendDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public GetDashboardTrendsHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
        => (_db, _currentUser, _dateTime) = (db, currentUser, dateTime);

    public async Task<Result<IEnumerable<DashboardTrendDto>>> Handle(GetDashboardTrendsQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin && !_currentUser.Roles.Contains("Agent") && !_currentUser.Roles.Contains("Supervisor"))
            return Result<IEnumerable<DashboardTrendDto>>.Failure("FORBIDDEN", "Access denied.");

        var from = _dateTime.Now.AddDays(-Math.Abs(request.Days));

        var metrics = await _db.TicketMetrics
            .Where(m => m.Date >= from.Date)
            .OrderBy(m => m.Date)
            .ToListAsync(ct);

        var result = metrics.Select(m => new DashboardTrendDto
        {
            Date     = m.Date,
            Created  = m.TotalCreated,
            Resolved = m.TotalResolved
        });

        return Result<IEnumerable<DashboardTrendDto>>.Success(result);
    }
}

public class GetAgentWorkloadHandler : IRequestHandler<GetAgentWorkloadQuery, Result<IEnumerable<AgentWorkloadDto>>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAgentWorkloadHandler(AppDbContext db, ICurrentUserService currentUser)
        => (_db, _currentUser) = (db, currentUser);

    public async Task<Result<IEnumerable<AgentWorkloadDto>>> Handle(GetAgentWorkloadQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin && !_currentUser.Roles.Contains("Agent") && !_currentUser.Roles.Contains("Supervisor"))
            return Result<IEnumerable<AgentWorkloadDto>>.Failure("FORBIDDEN", "Access denied.");

        var query = _db.SupportAgents.Where(a => a.IsActive && !a.IsDeleted);
        if (request.TeamId.HasValue)
            query = query.Where(a => a.TeamId == request.TeamId);

        var agents = await query.OrderBy(a => a.DisplayName).ToListAsync(ct);

        var result = agents.Select(a => new AgentWorkloadDto
        {
            AgentId              = a.Id,
            DisplayName          = a.DisplayName,
            Email                = a.Email,
            Tier                 = a.Tier,
            Availability         = a.Availability,
            CurrentTicketCount   = a.CurrentTicketCount,
            MaxConcurrentTickets = a.MaxConcurrentTickets,
            WorkloadPercent      = a.MaxConcurrentTickets > 0 ? Math.Round((double)a.CurrentTicketCount / a.MaxConcurrentTickets * 100, 1) : 0
        });

        return Result<IEnumerable<AgentWorkloadDto>>.Success(result);
    }
}
