using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SharedKernel.Services;

/// <summary>
/// No-op email service used until a real transport (SES/SMTP) is configured.
/// Logs the intent so emails are traceable in development and staging — never the variable
/// values, which carry 2FA codes and password-reset links.
/// </summary>
public class NullEmailService : IEmailService
{
    private readonly ILogger<NullEmailService> _logger;

    public NullEmailService(ILogger<NullEmailService> logger) => _logger = logger;

    public Task SendAsync(
        string toEmail,
        string toName,
        string languageCode,
        string eventType,
        Dictionary<string, string> variables,
        CancellationToken ct = default)
    {
        // Solo el evento, el destino enmascarado y los NOMBRES de las variables: este transporte se
        // elige solo cuando falta SES, también fuera de desarrollo, y los valores son códigos de
        // 2FA y enlaces de restablecer contraseña. El nombre del destinatario no se escribe: suele
        // ser el propio correo. Ver NotificationLogRedaction.
        _logger.LogInformation(
            "NullEmailService: would send '{EventType}' email to {ToEmail} [{Lang}]. Variables: {Vars}",
            eventType, NotificationLogRedaction.MaskEmail(toEmail), languageCode,
            NotificationLogRedaction.DescribeVariables(variables));

        return Task.CompletedTask;
    }
}
