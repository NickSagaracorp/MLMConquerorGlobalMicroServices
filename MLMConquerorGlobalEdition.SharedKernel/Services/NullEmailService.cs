using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SharedKernel.Services;

/// <summary>
/// No-op email service used until a real transport (SES/SMTP) is configured.
/// Logs the intent so emails are traceable in development and staging.
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
        _logger.LogInformation(
            "NullEmailService: would send '{EventType}' email to {ToName} <{ToEmail}> [{Lang}]. Variables: {Vars}",
            eventType, toName, toEmail, languageCode,
            string.Join(", ", variables.Select(kv => $"{kv.Key}={kv.Value}")));

        return Task.CompletedTask;
    }
}
