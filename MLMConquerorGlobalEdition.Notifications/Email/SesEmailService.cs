using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Domain.Entities.Email;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Notifications.Email;

/// <summary>
/// Envía correos transaccionales vía SES, resolviendo el asunto y el cuerpo del catálogo
/// EmailTemplate. La única llamada de red (<see cref="IEmailSender.SendAsync"/>) está aislada
/// detrás de una interfaz para que la resolución de plantilla, el respaldo de idioma y la
/// sustitución de variables se puedan probar sin tocar SES.
/// </summary>
public partial class SesEmailService : IEmailService
{
    private const string DefaultLanguage = "en";

    /// <summary>
    /// Marcador de plantilla: <c>{{VariableName}}</c>. Mismo estilo que TwilioSmsService, para
    /// que el catálogo SMS y el de correo no diverjan y nadie escriba el marcador equivocado
    /// al redactar plantillas en nueve idiomas.
    /// </summary>
    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex VariablePattern();

    private readonly AppDbContext _db;
    private readonly IEmailSender _sender;
    private readonly string _fromAddress;
    private readonly string _fromName;

    public SesEmailService(AppDbContext db, IEmailSender sender, IConfiguration config)
    {
        _db = db;
        _sender = sender;
        _fromAddress = config["Notifications:Email:Ses:FromAddress"]
            ?? throw new InvalidOperationException(
                "Missing configuration 'Notifications:Email:Ses:FromAddress'.");
        _fromName = config["Notifications:Email:Ses:FromName"]
            ?? throw new InvalidOperationException(
                "Missing configuration 'Notifications:Email:Ses:FromName'.");
    }

    public async Task SendAsync(
        string toEmail,
        string toName,
        string languageCode,
        string eventType,
        Dictionary<string, string> variables,
        CancellationToken ct = default)
    {
        var (subject, htmlBody, textBody) = await ResolveTemplateAsync(eventType, languageCode, ct);

        var renderedSubject = SubstituteVariables(subject, variables);
        var renderedHtmlBody = SubstituteVariables(htmlBody, variables);
        var renderedTextBody = textBody is null ? null : SubstituteVariables(textBody, variables);

        await _sender.SendAsync(
            _fromAddress, _fromName, toEmail, toName, renderedSubject, renderedHtmlBody, renderedTextBody, ct);
    }

    private async Task<(string Subject, string HtmlBody, string? TextBody)> ResolveTemplateAsync(
        string eventType, string languageCode, CancellationToken ct)
    {
        var template = await _db.EmailTemplates
            .AsNoTracking()
            .Include(t => t.Localizations)
            .FirstOrDefaultAsync(t => t.EventType == eventType && t.IsActive, ct);

        if (template is null)
            throw new InvalidOperationException(
                $"No active email template found for eventType '{eventType}'.");

        var localization = FindLocalization(template, languageCode)
            ?? FindLocalization(template, DefaultLanguage);

        if (localization is null)
            throw new InvalidOperationException(
                $"Email template for eventType '{eventType}' has no localization for '{languageCode}' " +
                $"or fallback '{DefaultLanguage}'.");

        return (localization.Subject, localization.HtmlBody, localization.TextBody);
    }

    private static EmailTemplateLocalization? FindLocalization(EmailTemplate template, string languageCode) =>
        template.Localizations.FirstOrDefault(l =>
            string.Equals(l.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase));

    private static string SubstituteVariables(string text, Dictionary<string, string> variables) =>
        VariablePattern().Replace(text, match =>
            variables.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
}
