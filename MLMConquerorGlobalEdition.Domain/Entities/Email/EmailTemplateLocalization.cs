using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Email;

public class EmailTemplateLocalization : AuditChangesIntKey
{
    public int EmailTemplateId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? TextBody { get; set; }

    public EmailTemplate? EmailTemplate { get; set; }
}
