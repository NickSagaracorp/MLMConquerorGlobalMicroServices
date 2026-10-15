using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Services;

namespace MLMConquerorGlobalEdition.Notifications.Sms;

/// <summary>
/// No-op SMS service used until Twilio credentials are configured.
/// Logs the intent so SMS sends are traceable in development and staging, and so a
/// half-configured deployment fails quietly instead of throwing on every send.
/// </summary>
public class NullSmsService : ISmsService
{
    private readonly ILogger<NullSmsService> _logger;

    public NullSmsService(ILogger<NullSmsService> logger) => _logger = logger;

    public Task SendAsync(
        string toPhoneE164,
        string languageCode,
        string eventType,
        Dictionary<string, string> variables,
        CancellationToken ct = default)
    {
        // Nunca los valores: este transporte se elige solo cuando faltan las credenciales de Twilio,
        // también fuera de desarrollo, y lo que viaja por SMS son códigos de 2FA y de step-up.
        // Ver NotificationLogRedaction.
        _logger.LogInformation(
            "NullSmsService: would send '{EventType}' SMS to {ToPhone} [{Lang}]. Variables: {Vars}",
            eventType, NotificationLogRedaction.MaskPhone(toPhoneE164), languageCode,
            NotificationLogRedaction.DescribeVariables(variables));

        return Task.CompletedTask;
    }
}
