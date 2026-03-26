using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Events;

public class CorporatePromo : AuditChangesStringKey
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? BannerUrl { get; set; }
    public bool IsActive { get; set; } = true;
}
