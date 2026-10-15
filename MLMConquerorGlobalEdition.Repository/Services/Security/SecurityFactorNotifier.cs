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

    /// <summary>
    /// Avisa al dueño de que se alcanzó el tope diario de códigos fallidos y esa verificación quedó
    /// bloqueada. Siempre por correo: si alguien está probando códigos de SMS, el teléfono no es un
    /// canal en el que fiarse para avisar.
    /// </summary>
    /// <param name="stepUp">True si lo bloqueado es el step-up; false si es el login.</param>
    Task NotifyTwoFactorLockedAsync(ApplicationUser user, bool stepUp, CancellationToken ct = default);
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

    public async Task NotifyTwoFactorLockedAsync(ApplicationUser user, bool stepUp, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning("Aviso de bloqueo del segundo factor sin destino para el usuario {UserId}.", user.Id);
            return;
        }

        try
        {
            var language = await TwoFactorLanguage.ResolveAsync(_db, user, ct) ?? "en";

            await _email.SendAsync(
                toEmail:      user.Email,
                toName:       user.Email,
                languageCode: language,
                eventType:    NotificationEvents.TwoFactorLocked,
                variables:    new Dictionary<string, string>
                {
                    ["Action"]   = DescribeLockedAction(stepUp, language),
                    // Hora de servidor, como el resto de marcas que ve el usuario.
                    ["LockedAt"] = _dateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                },
                ct:           ct);

            _logger.LogInformation("Aviso de bloqueo del segundo factor (step-up: {StepUp}) enviado al usuario {UserId}.",
                stepUp, user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo avisar del bloqueo del segundo factor (step-up: {StepUp}) al usuario {UserId}; el bloqueo se mantiene.",
                stepUp, user.Id);
        }
    }

    /// <summary>
    /// Qué quedó bloqueado, con palabras y en el idioma de la plantilla (en/es; el resto cae a la
    /// inglesa, así que el texto también va en inglés). Mismo criterio que <see cref="Describe"/>.
    /// </summary>
    internal static string DescribeLockedAction(bool stepUp, string language)
    {
        var es = language.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        return stepUp
            ? es ? "confirmar cambios sensibles (como a dónde se pagan tus comisiones)"
                 : "confirm sensitive changes (such as where your commissions are paid)"
            : es ? "iniciar sesión" : "sign in";
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
