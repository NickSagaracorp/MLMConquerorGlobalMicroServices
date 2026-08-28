namespace MLMConquerorGlobalEdition.Notifications.Email;

/// <summary>
/// Aísla la única llamada de red del envío por correo, para que la resolución de plantilla y
/// la sustitución de variables se puedan probar sin tocar SES.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(
        string fromAddress,
        string fromName,
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string? textBody,
        CancellationToken ct = default);
}
