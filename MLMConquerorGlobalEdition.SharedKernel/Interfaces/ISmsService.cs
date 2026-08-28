namespace MLMConquerorGlobalEdition.SharedKernel.Interfaces;

/// <summary>
/// Envía SMS transaccionales usando el catálogo SmsTemplate, igual que IEmailService usa
/// EmailTemplate: la implementación busca la plantilla por eventType + languageCode,
/// sustituye variables y entrega por el transporte configurado.
/// </summary>
public interface ISmsService
{
    /// <param name="toPhoneE164">Teléfono en formato E.164, ej. "+14155552671".</param>
    /// <param name="languageCode">Código ISO 639-1 (p. ej. "en", "es"). Cae a "en".</param>
    /// <param name="eventType">Coincide con las constantes de <see cref="NotificationEvents"/>.</param>
    /// <param name="variables">Sustituciones de la plantilla (clave → valor).</param>
    Task SendAsync(
        string toPhoneE164,
        string languageCode,
        string eventType,
        Dictionary<string, string> variables,
        CancellationToken ct = default);
}
