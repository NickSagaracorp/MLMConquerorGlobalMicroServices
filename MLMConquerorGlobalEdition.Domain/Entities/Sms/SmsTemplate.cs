using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Sms;

public class SmsTemplate : AuditChangesIntKey
{
    public string Name { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<SmsTemplateLocalization> Localizations { get; set; } = [];
}
