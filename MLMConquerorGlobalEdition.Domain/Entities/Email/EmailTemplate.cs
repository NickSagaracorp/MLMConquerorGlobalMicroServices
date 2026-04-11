using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Email;

public class EmailTemplate : AuditChangesIntKey
{
    public string Name { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<EmailTemplateLocalization> Localizations { get; set; } = [];
    public ICollection<EmailTemplateVariable> Variables { get; set; } = [];
}
