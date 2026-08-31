using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Server.Configuration;
using System.Text;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.EmailConfirmation;

/// <summary>
/// Emite el correo de confirmación de dirección. Hasta ahora nadie confirmaba nada: el registro
/// creaba el usuario con <c>EmailConfirmed = false</c> y solo los sembradores de desarrollo lo
/// ponían en true.
/// </summary>
public class SendEmailConfirmationHandler : IRequestHandler<SendEmailConfirmationCommand, Result<bool>>
{
    private const string DefaultPortalBaseUrl      = "https://localhost:7004";
    private const string DefaultAdminPortalBaseUrl = "https://localhost:7001";
    private const string DefaultLanguage           = "en";

    private readonly UserManager<ApplicationUser>          _userManager;
    private readonly IEmailService                         _email;
    private readonly AppDbContext                          _db;
    private readonly IConfiguration                        _config;
    private readonly ILogger<SendEmailConfirmationHandler> _logger;

    public SendEmailConfirmationHandler(
        UserManager<ApplicationUser>          userManager,
        IEmailService                         email,
        AppDbContext                          db,
        IConfiguration                        config,
        ILogger<SendEmailConfirmationHandler> logger)
    {
        _userManager = userManager;
        _email       = email;
        _db          = db;
        _config      = config;
        _logger      = logger;
    }

    public async Task<Result<bool>> Handle(SendEmailConfirmationCommand command, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(command.Email);

        // Siempre éxito, exista o no la cuenta, esté confirmada o no. Un endpoint que responde
        // distinto en cada caso es un oráculo para enumerar usuarios registrados: se le prueba
        // una lista de correos y las respuestas dicen cuáles existen. Mismo criterio que
        // ForgotPasswordHandler.
        if (user is null || !user.IsActive || user.EmailConfirmed || string.IsNullOrEmpty(user.Email))
            return Result<bool>.Success(true);

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

        // El token de Identity contiene '+', '/' y '=' — caracteres que se corrompen al viajar
        // en una query string ('+' se decodifica como espacio, '/' y '=' confunden a proxies y
        // clientes de correo). En base64url no aparece ninguno de los tres. Sin esta
        // codificación el enlace funcionaría en unas cuentas y en otras no, según qué
        // caracteres salieran en el token: un fallo intermitente carísimo de diagnosticar.
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var (languageCode, displayName) = await ResolveRecipientAsync(user, ct);

        // La cifra sale de EmailLinkLifetime, que es la MISMA fuente con la que Program.cs
        // configura TokenLifespan del proveedor de Identity. Mismo criterio que
        // ForgotPasswordHandler: el correo no puede anunciar una caducidad que el token no tenga.
        var variables = new Dictionary<string, string>
        {
            ["ConfirmationUrl"]  = BuildConfirmationUrl(user, encodedToken),
            ["ExpiresInMinutes"] = EmailLinkLifetime.Minutes(_config).ToString()
        };

        try
        {
            await _email.SendAsync(
                user.Email, displayName, languageCode,
                NotificationEvents.EmailConfirmation, variables, ct);
        }
        catch (Exception ex)
        {
            // Que el transporte falle no puede cambiar la respuesta: una excepción que solo se
            // produce cuando la cuenta existe reintroduciría por la puerta de atrás el mismo
            // oráculo de enumeración que este endpoint evita. Se registra y se sigue.
            _logger.LogError(ex,
                "No se pudo enviar el correo de confirmación al usuario {UserId}.", user.Id);
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// El staff no tiene <c>MemberProfileId</c> (ver <see cref="ApplicationUser"/>) y su enlace
    /// apunta al portal de administración; los ambassadors y miembros, al BizCenter. Mandar a
    /// alguien al portal equivocado lo deja en una pantalla de inicio de sesión que no es la suya.
    /// </summary>
    private string BuildConfirmationUrl(ApplicationUser user, string encodedToken)
    {
        var isStaff = string.IsNullOrEmpty(user.MemberProfileId);

        var baseUrl = isStaff
            ? _config["Auth:AdminPortalBaseUrl"] ?? DefaultAdminPortalBaseUrl
            : _config["Auth:PortalBaseUrl"]      ?? DefaultPortalBaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = isStaff ? DefaultAdminPortalBaseUrl : DefaultPortalBaseUrl;

        return $"{baseUrl.TrimEnd('/')}/auth/confirm-email" +
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
