using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

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
        _logger.LogInformation(
            "NullSmsService: would send '{EventType}' SMS to {ToPhone} [{Lang}]. Variables: {Vars}",
            eventType, toPhoneE164, languageCode,
            string.Join(", ", variables.Select(kv => $"{kv.Key}={kv.Value}")));

        return Task.CompletedTask;
    }
}
