using System.Globalization;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Security;

/// <summary>Qué cambió, para que el aviso lo diga con palabras y no con un código.</summary>
public enum SecurityFactorChange
{
    Email,
    Phone,
    Authenticator,
    Channel,
    TwoFactorDisabled
}

/// <summary>
/// Avisa al dueño de la cuenta de que cambió un canal por el que recibe códigos.
/// </summary>
public interface ISecurityFactorNotifier
{
    /// <param name="user">La cuenta. Da el idioma y, si no se indica otro, el destino.</param>
    /// <param name="change">Lo que cambió.</param>
    /// <param name="toEmail">
    /// Otro destino que el correo actual. Al cambiar el correo el aviso va al ANTERIOR: el nuevo
    /// puede ser justo el de quien robó la sesión, y avisarle a él no protege a nadie.
    /// </param>
    Task NotifyAsync(ApplicationUser user, SecurityFactorChange change, string? toEmail = null, CancellationToken ct = default);
}

/// <inheritdoc cref="ISecurityFactorNotifier"/>
/// <remarks>
/// <b>NUNCA LANZA.</b> El cambio ya está guardado cuando se llama aquí, y un fallo de SES no puede
/// convertir en error una operación que sí ocurrió: la pantalla diría "falló" sobre un correo que ya
/// cambió. El fallo se registra, que es lo que permite enterarse de que el aviso no salió.
///
/// Vive en Repository y no en cada anfitrión porque lo usan SignupAPI (teléfono, autenticador,
/// canal) y el BizCenter (correo): el texto del cambio y la forma de fallar son una sola cosa.
/// </remarks>
public sealed class SecurityFactorNotifier : ISecurityFactorNotifier
{
    private readonly IEmailService                    _email;
    private readonly AppDbContext                     _db;
    private readonly IDateTimeProvider                _dateTime;
    private readonly ILogger<SecurityFactorNotifier>  _logger;

    public SecurityFactorNotifier(
        IEmailService                   email,
        AppDbContext                    db,
        IDateTimeProvider               dateTime,
        ILogger<SecurityFactorNotifier> logger)
    {
        _email    = email;
        _db       = db;
        _dateTime = dateTime;
        _logger   = logger;
    }

    public async Task NotifyAsync(
        ApplicationUser user, SecurityFactorChange change, string? toEmail = null, CancellationToken ct = default)
    {
        var to = string.IsNullOrWhiteSpace(toEmail) ? user.Email : toEmail;
        if (string.IsNullOrWhiteSpace(to))
        {
            _logger.LogWarning("Aviso de cambio de factor {Change} sin destino para el usuario {UserId}.", change, user.Id);
            return;
        }

        try
        {
            var language = await TwoFactorLanguage.ResolveAsync(_db, user, ct) ?? "en";

            await _email.SendAsync(
                toEmail:      to,
                toName:       to,
                languageCode: language,
                eventType:    NotificationEvents.SecurityFactorChanged,
                variables:    new Dictionary<string, string>
                {
                    ["Change"]    = Describe(change, language),
                    // Hora de servidor, como la marca que acaba de sellarse.
                    ["ChangedAt"] = _dateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                },
                ct:           ct);

            _logger.LogInformation("Aviso de cambio de factor {Change} enviado al usuario {UserId}.", change, user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo avisar del cambio de factor {Change} al usuario {UserId}; el cambio se mantiene.",
                change, user.Id);
        }
    }

    /// <summary>
    /// El texto del cambio en el idioma de la plantilla. Solo hay plantilla en inglés y español; para
    /// cualquier otro idioma el transporte entrega la inglesa, así que el texto también va en inglés
    /// y no queda una frase en un idioma dentro de un correo en otro.
    /// </summary>
    internal static string Describe(SecurityFactorChange change, string language)
    {
        var es = language.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        return change switch
        {
            SecurityFactorChange.Email             => es ? "el correo electrónico de la cuenta"
                                                         : "the email address of the account",
            SecurityFactorChange.Phone             => es ? "el teléfono que recibe tus códigos de verificación"
                                                         : "the phone number that receives your verification codes",
            SecurityFactorChange.Authenticator     => es ? "la aplicación de autenticación vinculada a la cuenta"
                                                         : "the authenticator app linked to the account",
            SecurityFactorChange.Channel           => es ? "el medio por el que recibes tus códigos de verificación"
                                                         : "how you receive your verification codes",
            SecurityFactorChange.TwoFactorDisabled => es ? "la verificación en dos pasos (se desactivó)"
                                                         : "two-step verification (it was turned off)",
            _                                      => es ? "un dato de seguridad de la cuenta"
                                                         : "a security setting of the account"
        };
    }
}
