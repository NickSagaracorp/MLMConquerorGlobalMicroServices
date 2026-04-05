using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Support;

public class SlaBreach : AuditChangesLongKey
{
    public string TicketId { get; set; } = string.Empty;
    public string SlaPolicyId { get; set; } = string.Empty;
    public SlaMetricType MetricType { get; set; }
    public DateTime DueAt { get; set; }
    public DateTime BreachedAt { get; set; }
    public int BreachDurationMinutes { get; set; }
    public string? AssignedAgentId { get; set; }
    public int? AssignedTeamId { get; set; }

    public Ticket Ticket { get; set; } = null!;
}
