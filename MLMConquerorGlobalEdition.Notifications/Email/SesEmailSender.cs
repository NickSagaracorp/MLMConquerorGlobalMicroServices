using Amazon;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Configuration;

namespace MLMConquerorGlobalEdition.Notifications.Email;

/// <summary>
/// Transporte real de SES para <see cref="IEmailSender"/>. Construye el cliente del SDK con
/// la cadena de proveedores de credenciales por defecto (rol de instancia, variables de
/// entorno o perfil) — nunca lee AWS:Credentials de configuración, igual que <c>IAmazonS3</c>
/// en SignupAPI/Program.cs. Nunca se ejercita en las pruebas — esas usan un fake sender para
/// que la suite no toque la red.
/// </summary>
public class SesEmailSender : IEmailSender
{
    private readonly AmazonSimpleEmailServiceV2Client _client;

    public SesEmailSender(IConfiguration config)
    {
        var region = config["Notifications:Email:Ses:Region"] ?? "us-east-1";
        _client = new AmazonSimpleEmailServiceV2Client(RegionEndpoint.GetBySystemName(region));
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
        var body = new Body
        {
            Html = new Content { Data = htmlBody, Charset = "UTF-8" }
        };

        if (!string.IsNullOrWhiteSpace(textBody))
            body.Text = new Content { Data = textBody, Charset = "UTF-8" };

        var request = new SendEmailRequest
        {
            FromEmailAddress = $"{fromName} <{fromAddress}>",
            Destination = new Destination
            {
                ToAddresses = [$"{toName} <{toEmail}>"]
            },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = subject, Charset = "UTF-8" },
                    Body = body
                }
            }
        };

        await _client.SendEmailAsync(request, ct);
    }
}
