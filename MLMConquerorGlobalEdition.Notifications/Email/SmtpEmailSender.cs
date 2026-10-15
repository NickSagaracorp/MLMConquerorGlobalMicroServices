using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace MLMConquerorGlobalEdition.Notifications.Email;

/// <summary>
/// Transporte SMTP para <see cref="IEmailSender"/>, pensado para desarrollo contra un buzón de
/// captura como MailHog (<c>localhost:1025</c>, bandeja en <c>http://localhost:8025</c>).
/// </summary>
/// <remarks>
/// Comparte con SES todo lo demás —plantilla, idioma, variables y remitente— porque lo arma
/// <see cref="SesEmailService"/>; aquí solo cambia la última milla. Así lo que se ve en MailHog es
/// exactamente lo que SES mandaría.
///
/// Sin autenticación ni TLS: un buzón de captura no los pide, y un SMTP real de producción no se
/// configura por aquí sino con SES.
/// </remarks>
public class SmtpEmailSender : IEmailSender
{
    private readonly string _host;
    private readonly int    _port;

    public SmtpEmailSender(IConfiguration config)
    {
        _host = config["Notifications:Email:Smtp:Host"] ?? "localhost";
        _port = int.TryParse(config["Notifications:Email:Smtp:Port"], out var port) ? port : 1025;
    }

    public async Task SendAsync(
        string fromAddress,
        string fromName,
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string? textBody,
        CancellationToken ct = default)
    {
        using var message = new MailMessage
        {
            From            = new MailAddress(fromAddress, fromName),
            Subject         = subject,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding    = Encoding.UTF8
        };
        message.To.Add(new MailAddress(toEmail, toName));

        // Las dos versiones, igual que SES: el cliente de correo elige la que sabe mostrar.
        if (!string.IsNullOrWhiteSpace(textBody))
            message.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(textBody, Encoding.UTF8, MediaTypeNames.Text.Plain));
        message.AlternateViews.Add(
            AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, MediaTypeNames.Text.Html));

        using var client = new SmtpClient(_host, _port) { EnableSsl = false };
        await client.SendMailAsync(message, ct);
    }
}
