using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Support;

public class TicketCategory : AuditChangesIntKey
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentCategoryId { get; set; }
    public int? DefaultTeamId { get; set; }
    public string DefaultPriority { get; set; } = "Normal";
    public string? DefaultSlaPolicyId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public TicketCategory? ParentCategory { get; set; }
    public ICollection<TicketCategory> SubCategories { get; set; } = new List<TicketCategory>();
    public SupportTeam? DefaultTeam { get; set; }
    public SlaPolicy? DefaultSlaPolicy { get; set; }
}
