using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Email;

public class EmailTemplateVariable : AuditChangesIntKey
{
    public int EmailTemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsRequired { get; set; } = false;

    public EmailTemplate? EmailTemplate { get; set; }
}
