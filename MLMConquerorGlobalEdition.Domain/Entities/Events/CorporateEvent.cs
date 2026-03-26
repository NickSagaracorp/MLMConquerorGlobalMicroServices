using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Events;

public class CorporateEvent : AuditChangesStringKey
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string? Location { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}
