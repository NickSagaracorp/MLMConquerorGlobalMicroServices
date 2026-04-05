using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Services;

/// <summary>
/// Calculates SLA deadlines in calendar minutes (simplified — does not model business-hours gaps).
/// Business-hours-aware calculations can be added in a future iteration without changing the interface.
/// </summary>
public class SlaMonitorService : ISlaMonitorService
{
    public void AssignSlaDeadlines(Ticket ticket, SlaPolicy policy, DateTime utcNow)
    {
        var frtMinutes = GetFirstResponseTarget(policy, ticket.Priority);
        var resMinutes = GetResolutionTarget(policy, ticket.Priority);

        ticket.SlaFirstResponseDue = utcNow.AddMinutes(frtMinutes);
        ticket.SlaResolutionDue    = utcNow.AddMinutes(resMinutes);
        ticket.IsSlaPaused         = false;
        ticket.SlaPausedMinutes    = 0;
    }

    public void PauseTimer(Ticket ticket, DateTime utcNow)
    {
        if (ticket.IsSlaPaused) return;
        ticket.IsSlaPaused  = true;
        ticket.SlaPausedAt  = utcNow;
    }

    public void ResumeTimer(Ticket ticket, DateTime utcNow)
    {
        if (!ticket.IsSlaPaused || ticket.SlaPausedAt is null) return;

        var pausedFor = (utcNow - ticket.SlaPausedAt.Value).TotalMinutes;
        ticket.SlaPausedMinutes += pausedFor;

        // Extend deadlines by the paused duration
        ticket.SlaFirstResponseDue = ticket.SlaFirstResponseDue?.AddMinutes(pausedFor);
        ticket.SlaResolutionDue    = ticket.SlaResolutionDue?.AddMinutes(pausedFor);

        ticket.IsSlaPaused = false;
        ticket.SlaPausedAt = null;
    }

    public double CalculateRemainingResolutionMinutes(Ticket ticket, DateTime utcNow)
    {
        if (ticket.IsSlaPaused || ticket.SlaResolutionDue is null) return double.MaxValue;
        return (ticket.SlaResolutionDue.Value - utcNow).TotalMinutes;
    }

    public double CalculatePercentElapsed(Ticket ticket, DateTime utcNow)
    {
        if (ticket.SlaResolutionDue is null || ticket.SlaPolicyId is null) return 0;

        var remaining  = CalculateRemainingResolutionMinutes(ticket, utcNow);
        var totalDuration = (ticket.SlaResolutionDue.Value - ticket.CreationDate).TotalMinutes;

        if (totalDuration <= 0) return 100;

        var elapsed = totalDuration - remaining;
        return Math.Min((elapsed / totalDuration) * 100, 150);
    }

    public int GetFirstResponseTarget(SlaPolicy policy, TicketPriority priority) => priority switch
    {
        TicketPriority.Critical => policy.FirstResponseCriticalMinutes,
        TicketPriority.High     => policy.FirstResponseHighMinutes,
        TicketPriority.Low      => policy.FirstResponseLowMinutes,
        _                       => policy.FirstResponseNormalMinutes
    };

    public int GetResolutionTarget(SlaPolicy policy, TicketPriority priority) => priority switch
    {
        TicketPriority.Critical => policy.ResolutionCriticalMinutes,
        TicketPriority.High     => policy.ResolutionHighMinutes,
        TicketPriority.Low      => policy.ResolutionLowMinutes,
        _                       => policy.ResolutionNormalMinutes
    };
}
