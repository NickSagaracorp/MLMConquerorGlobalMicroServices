using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Services;

public interface ISlaMonitorService
{
    /// <summary>Sets SlaFirstResponseDue and SlaResolutionDue on the ticket based on its policy and priority.</summary>
    void AssignSlaDeadlines(Ticket ticket, SlaPolicy policy, DateTime utcNow);

    /// <summary>Returns the remaining minutes before the resolution SLA is breached (negative = already breached).</summary>
    double CalculateRemainingResolutionMinutes(Ticket ticket, DateTime utcNow);

    /// <summary>Returns percent of SLA elapsed (0-100+).</summary>
    double CalculatePercentElapsed(Ticket ticket, DateTime utcNow);

    /// <summary>Pauses the SLA timer (status changed to waiting_customer).</summary>
    void PauseTimer(Ticket ticket, DateTime utcNow);

    /// <summary>Resumes the SLA timer and adjusts deadlines by the paused duration.</summary>
    void ResumeTimer(Ticket ticket, DateTime utcNow);

    /// <summary>Returns the first-response target in minutes for the ticket's priority.</summary>
    int GetFirstResponseTarget(SlaPolicy policy, TicketPriority priority);

    /// <summary>Returns the resolution target in minutes for the ticket's priority.</summary>
    int GetResolutionTarget(SlaPolicy policy, TicketPriority priority);
}
