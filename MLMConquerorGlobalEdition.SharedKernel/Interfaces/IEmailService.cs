namespace MLMConquerorGlobalEdition.SharedKernel.Interfaces;

/// <summary>
/// Sends transactional emails using the EmailTemplate catalog.
/// Implementations look up the localized template by eventType + languageCode,
/// substitute variables, and deliver via the configured transport (SES, SMTP, etc.).
/// </summary>
public interface IEmailService
{
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="toName">Recipient display name for the To: header.</param>
    /// <param name="languageCode">ISO 639-1 language code (e.g. "en", "es"). Falls back to "en".</param>
    /// <param name="eventType">Matches <see cref="NotificationEvents"/> constants and EmailTemplate.EventType.</param>
    /// <param name="variables">Template variable substitutions (key → value).</param>
    Task SendAsync(
        string toEmail,
        string toName,
        string languageCode,
        string eventType,
        Dictionary<string, string> variables,
        CancellationToken ct = default);
}
