using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Sms;

public class SmsTemplateLocalization : AuditChangesIntKey
{
    public int SmsTemplateId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>Cuerpo del mensaje. Máximo 480 caracteres: tres segmentos GSM-7.
    /// Más allá de eso Twilio cobra por segmento adicional y algunos operadores truncan.</summary>
    public string Body { get; set; } = string.Empty;

    public SmsTemplate? SmsTemplate { get; set; }
}
