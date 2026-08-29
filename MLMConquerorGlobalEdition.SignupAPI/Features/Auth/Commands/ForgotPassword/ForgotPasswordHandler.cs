using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using System.Text;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ForgotPassword;

/// <summary>
/// Emite el correo de recuperación de contraseña. Hasta ahora este camino generaba el token de
/// Identity y lo tiraba: el correo nunca salía, así que nadie ha podido recuperar nunca su
/// contraseña por su cuenta.
/// </summary>
/// <remarks>
/// Calcado de <c>SendEmailConfirmationHandler</c>, que resolvió el mismo problema: token en
/// base64url, URL base según el portal del destinatario, y la misma respuesta pase lo que pase.
/// </remarks>
public class ForgotPasswordHandler : IRequestHandler<ForgotPasswordCommand, Result<bool>>
{
    /// <summary>
    /// Vigencia del token de Identity. Es el valor por defecto de <c>DataProtectionTokenProvider</c>
    /// (<c>TokenLifespan</c> = 1 día); se declara aquí porque el correo se lo dice al usuario y
    /// las dos cifras tienen que coincidir.
    /// </summary>
    public const int ExpiresInHours = 24;

    private const string DefaultPortalBaseUrl      = "https://localhost:7004";
    private const string DefaultAdminPortalBaseUrl = "https://localhost:7001";
    private const string DefaultLanguage           = "en";

    private readonly UserManager<ApplicationUser>    _userManager;
    private readonly IEmailService                   _email;
    private readonly AppDbContext                    _db;
    private readonly IConfiguration                  _config;
    private readonly ILogger<ForgotPasswordHandler>  _logger;

    public ForgotPasswordHandler(
        UserManager<ApplicationUser>   userManager,
        IEmailService                  email,
        AppDbContext                   db,
        IConfiguration                 config,
        ILogger<ForgotPasswordHandler> logger)
    {
        _userManager = userManager;
        _email       = email;
        _db          = db;
        _config      = config;
        _logger      = logger;
    }

    public async Task<Result<bool>> Handle(ForgotPasswordCommand command, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(command.Email);

        // Siempre éxito, exista o no la cuenta. Un endpoint que responde distinto en cada caso es
        // un oráculo para enumerar usuarios registrados: se le prueba una lista de correos y las
        // respuestas dicen cuáles existen.
        if (user is null || !user.IsActive || string.IsNullOrEmpty(user.Email))
            return Result<bool>.Success(true);

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        // El token de Identity contiene '+', '/' y '=' — caracteres que se corrompen al viajar
        // en una query string ('+' se decodifica como espacio, '/' y '=' confunden a proxies y
        // clientes de correo). En base64url no aparece ninguno de los tres. Sin esta
        // codificación el enlace funcionaría en unas cuentas y en otras no, según qué caracteres
        // salieran en el token: un fallo intermitente carísimo de diagnosticar.
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var (languageCode, displayName) = await ResolveRecipientAsync(user, ct);

        var variables = new Dictionary<string, string>
        {
            ["ResetUrl"]       = BuildResetUrl(user, encodedToken),
            ["ExpiresInHours"] = ExpiresInHours.ToString()
        };

        try
        {
            await _email.SendAsync(
                user.Email, displayName, languageCode,
                NotificationEvents.PasswordReset, variables, ct);
        }
        catch (Exception ex)
        {
            // Que el transporte falle no puede cambiar la respuesta. Una excepción que solo se
            // produce cuando la cuenta existe reintroduciría por la puerta de atrás el mismo
            // oráculo de enumeración que este endpoint evita: bastaría con mirar cuáles de los
            // correos probados devuelven 500. Se registra y se sigue.
            _logger.LogError(ex,
                "No se pudo enviar el correo de recuperación de contraseña al usuario {UserId}.", user.Id);
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// El staff no tiene <c>MemberProfileId</c> y su enlace apunta al portal de administración;
    /// los ambassadors y miembros, al BizCenter. Mandar a alguien al portal equivocado lo deja en
    /// una pantalla de inicio de sesión que no es la suya.
    /// </summary>
    /// <remarks>
    /// El enlace lleva <c>userId</c> y no <c>email</c>. La dirección en la query se queda en el
    /// historial del navegador, en los registros de cualquier proxy por el que pase y en la
    /// cabecera <c>Referer</c> que la página manda a todo recurso externo que cargue — tipografías,
    /// analítica, imágenes. Un identificador opaco no dice nada de esos.
    /// </remarks>
    private string BuildResetUrl(ApplicationUser user, string encodedToken)
    {
        var isStaff = string.IsNullOrEmpty(user.MemberProfileId);

        var baseUrl = isStaff
            ? _config["Auth:AdminPortalBaseUrl"] ?? DefaultAdminPortalBaseUrl
            : _config["Auth:PortalBaseUrl"]      ?? DefaultPortalBaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = isStaff ? DefaultAdminPortalBaseUrl : DefaultPortalBaseUrl;

        return $"{baseUrl.TrimEnd('/')}/auth/reset-password" +
               $"?userId={Uri.EscapeDataString(user.Id)}&token={encodedToken}";
    }

    /// <summary>
    /// Idioma y nombre para el encabezado To:. El staff no tiene MemberProfile, así que cae al
    /// idioma por defecto — SesEmailService respalda a "en" cuando falta la localización.
    /// </summary>
    private async Task<(string LanguageCode, string DisplayName)> ResolveRecipientAsync(
        ApplicationUser user, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(user.MemberProfileId))
            return (DefaultLanguage, user.Email!);

        var profile = await _db.MemberProfiles.AsNoTracking()
            .Where(m => m.MemberId == user.MemberProfileId)
            .Select(m => new { m.FirstName, m.LastName, m.DefaultLanguage })
            .FirstOrDefaultAsync(ct);

        if (profile is null)
            return (DefaultLanguage, user.Email!);

        var name = $"{profile.FirstName} {profile.LastName}".Trim();

        return (string.IsNullOrWhiteSpace(profile.DefaultLanguage) ? DefaultLanguage : profile.DefaultLanguage,
                string.IsNullOrWhiteSpace(name) ? user.Email! : name);
    }
}
