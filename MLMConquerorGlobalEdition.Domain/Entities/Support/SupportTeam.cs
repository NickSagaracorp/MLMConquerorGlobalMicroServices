using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Support;

public class SupportTeam : AuditChangesIntKey
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SupervisorAgentId { get; set; }
    public string RoutingMethod { get; set; } = "round_robin";  // round_robin | least_busy | manual
    public bool IsActive { get; set; } = true;

    public ICollection<SupportAgent> Agents { get; set; } = new List<SupportAgent>();
}
