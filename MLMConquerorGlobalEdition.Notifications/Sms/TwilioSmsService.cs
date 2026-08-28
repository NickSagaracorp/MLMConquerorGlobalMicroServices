using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Domain.Entities.Sms;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Notifications.Sms;

/// <summary>
/// Sends transactional SMS via Twilio, resolving the message body from the SmsTemplate
/// catalog. The only network call (<see cref="ITwilioMessageSender.SendAsync"/>) is isolated
/// behind an interface so template resolution, language fallback, variable substitution and
/// phone validation can be tested without touching Twilio.
/// </summary>
public partial class TwilioSmsService : ISmsService
{
    private const string DefaultLanguage = "en";

    /// <summary>
    /// Marker style used by the SmsTemplate catalog: <c>{{VariableName}}</c>. There is no
    /// existing precedent for a substitution marker elsewhere in the repo (no IEmailService
    /// implementation exists yet, only NullEmailService), so this was chosen to match what
    /// NotificationEvents documents as the variable dictionary keys (e.g. "Code",
    /// "ExpiresInMinutes"). Whoever implements the email transport should reuse this same
    /// style so the two catalogs stay consistent.
    /// </summary>
    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex VariablePattern();

    private readonly AppDbContext _db;
    private readonly ITwilioMessageSender _sender;
    private readonly string _fromNumber;

    public TwilioSmsService(AppDbContext db, ITwilioMessageSender sender, IConfiguration config)
    {
        _db = db;
        _sender = sender;
        _fromNumber = config["Notifications:Sms:Twilio:FromNumber"]
            ?? throw new InvalidOperationException(
                "Missing configuration 'Notifications:Sms:Twilio:FromNumber'.");
    }

    public async Task SendAsync(
        string toPhoneE164,
        string languageCode,
        string eventType,
        Dictionary<string, string> variables,
        CancellationToken ct = default)
    {
        // El teléfono se valida antes de tocar la base de datos o la plantilla: un envío mal
        // formado no debe ni siquiera consultar el catálogo, y mucho menos llamar a Twilio.
        ValidatePhone(toPhoneE164);

        var body = await ResolveBodyAsync(eventType, languageCode, ct);
        var rendered = SubstituteVariables(body, variables);

        await _sender.SendAsync(_fromNumber, toPhoneE164, rendered, ct);
    }

    /// <summary>
    /// La regla vive en <see cref="PhoneNumberFormat"/>, no aquí: quien da de alta un teléfono
    /// valida con la misma, y si divergieran aceptaríamos allí números que fallarían aquí.
    /// </summary>
    private static void ValidatePhone(string toPhoneE164)
    {
        if (!PhoneNumberFormat.IsE164(toPhoneE164))
            throw new ArgumentException(
                $"'{toPhoneE164}' is not a valid E.164 phone number.", nameof(toPhoneE164));
    }

    private async Task<string> ResolveBodyAsync(string eventType, string languageCode, CancellationToken ct)
    {
        var template = await _db.SmsTemplates
            .AsNoTracking()
            .Include(t => t.Localizations)
            .FirstOrDefaultAsync(t => t.EventType == eventType && t.IsActive, ct);

        if (template is null)
            throw new InvalidOperationException(
                $"No active SMS template found for eventType '{eventType}'.");

        var localization = FindLocalization(template, languageCode)
            ?? FindLocalization(template, DefaultLanguage);

        if (localization is null)
            throw new InvalidOperationException(
                $"SMS template for eventType '{eventType}' has no localization for '{languageCode}' " +
                $"or fallback '{DefaultLanguage}'.");

        return localization.Body;
    }

    private static SmsTemplateLocalization? FindLocalization(SmsTemplate template, string languageCode) =>
        template.Localizations.FirstOrDefault(l =>
            string.Equals(l.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase));

    private static string SubstituteVariables(string body, Dictionary<string, string> variables) =>
        VariablePattern().Replace(body, match =>
            variables.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
}
