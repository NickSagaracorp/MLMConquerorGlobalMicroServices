using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// Comprueba la firma, el emisor, la audiencia y la vigencia del token de acceso antes de que el
/// portal monte con sus claims la cookie de sesión.
/// </summary>
/// <remarks>
/// El token llega de SignupAPI por una llamada de servidor a servidor, así que antes bastaba con
/// leerlo. Pero de esos claims sale el principal de la cookie —roles incluidos—, y la cookie es lo
/// que el portal mira en cada petición. Leerlo sin validar dejaba que cualquier respuesta con
/// forma de token (una dirección de API mal configurada, un proxy en medio) abriera sesión con los
/// roles que quisiera. Con la llave pública del par, solo entra lo que firmó nuestro emisor.
///
/// Un reto de 2FA no pasa aunque esté bien firmado: lleva otra audiencia y además el claim de
/// propósito, igual que en las API.
/// </remarks>
public sealed class PortalAccessTokenValidator
{
    private readonly TokenValidationParameters _parameters;

    public PortalAccessTokenValidator(SecurityKey signingKey, string issuer, string audience)
    {
        _parameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = issuer,
            ValidAudience            = audience,
            IssuerSigningKey         = signingKey,
            ValidAlgorithms          = [SecurityAlgorithms.RsaSha256],

            // Treinta segundos: el portal y SignupAPI son máquinas distintas y sus relojes no
            // coinciden al milisegundo. Las API usan cero porque el token lo valida el mismo
            // reloj durante toda su vida; aquí se valida una vez, recién emitido.
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Arma el validador con <c>Jwt:PublicKeyBase64</c>, <c>Jwt:Issuer</c> y <c>Jwt:Audience</c>.
    /// Falla al arrancar si falta cualquiera de los tres o si la llave es la revocada.
    /// </summary>
    public static PortalAccessTokenValidator FromConfiguration(IConfiguration configuration)
    {
        var publicKeyBase64 = JwtKeyGuard.ValidatePublicKey(configuration["Jwt:PublicKeyBase64"]);
        var issuer   = Required(configuration, "Jwt:Issuer");
        var audience = Required(configuration, "Jwt:Audience");

        var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
        return new PortalAccessTokenValidator(new RsaSecurityKey(rsa), issuer, audience);
    }

    /// <summary>
    /// El token, si es válido; null si no lo es por cualquier motivo. Los claims se devuelven tal
    /// como vienen en el token, sin el mapeo de nombres de .NET, igual que los leía el portal.
    /// </summary>
    public JwtSecurityToken? Validate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        try
        {
            handler.ValidateToken(token, _parameters, out var validated);
            var jwt = validated as JwtSecurityToken;
            return jwt is null || ChallengeAudience.CarriesPurpose(jwt.Claims) ? null : jwt;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }

    private static string Required(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"{key} no está configurada. El portal la necesita para validar el token de acceso " +
                "que emite SignupAPI; tiene que ser el mismo valor que en SignupAPI.");
        return value;
    }
}
