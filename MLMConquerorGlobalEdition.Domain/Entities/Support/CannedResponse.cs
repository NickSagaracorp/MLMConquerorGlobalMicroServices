using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Support;

public class CannedResponse : AuditChangesStringKey
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;   // supports {{customerName}}, {{ticketNumber}}, {{agentName}}, {{category}}, {{createdDate}}
    public string? Category { get; set; }
    public string TagsJson { get; set; } = "[]";
    public string Scope { get; set; } = "global";      // global | team | personal
    public string? OwnerAgentId { get; set; }          // set when scope = personal
    public int? TeamId { get; set; }                   // set when scope = team
    public int UsageCount { get; set; }
    public bool IsActive { get; set; } = true;

    public SupportTeam? Team { get; set; }
}
