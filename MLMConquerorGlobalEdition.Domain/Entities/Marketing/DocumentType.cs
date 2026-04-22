using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Marketing;

public class DocumentType : AuditChangesIntKey
{
    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int     SortOrder   { get; set; }
    public bool    IsActive    { get; set; } = true;

    public ICollection<MarketingDocument> Documents { get; set; } = new List<MarketingDocument>();
}
