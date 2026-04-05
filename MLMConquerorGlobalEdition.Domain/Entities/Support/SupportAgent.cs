using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Support;

public class SupportAgent : AuditChangesStringKey
{
    public string UserId { get; set; } = string.Empty;           // FK to ApplicationUser
    public string MemberId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int? TeamId { get; set; }
    public int Tier { get; set; } = 1;                           // 1 | 2 | 3
    public string SkillsJson { get; set; } = "[]";
    public string LanguagesJson { get; set; } = "[\"es\"]";
    public int MaxConcurrentTickets { get; set; } = 10;
    public int CurrentTicketCount { get; set; }
    public string Availability { get; set; } = "available";      // available | busy | offline
    public bool IsActive { get; set; } = true;

    public SupportTeam? Team { get; set; }
}
