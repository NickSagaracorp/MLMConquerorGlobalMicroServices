using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.Authn.Services;

/// <summary>
/// El emisor de TOKENS DE ACCESO. Uno solo para toda la solución.
/// </summary>
/// <remarks>
/// POR QUÉ ESTÁ AQUÍ Y NO EN CADA API. Hasta ahora había dos copias —una en AdminAPI y otra en
/// SignupAPI— idénticas salvo por el espaciado y por un claim de idioma que solo tenía la de
/// SignupAPI. Dos copias de la clase que FIRMA las credenciales de la casa son dos sitios donde
/// arreglar el mismo fallo y, peor, dos sitios que pueden divergir sin que nada se ponga rojo:
/// la copia de AdminAPI ya había perdido el claim <c>default_language</c> y nadie se enteró.
///
/// POR QUÉ EN Authn Y NO EN SharedKernel NI EN SharedKernel.Server. En SharedKernel no puede
/// estar: ese proyecto es la base de ClientCore y de las MAUI, y el código que carga una llave
/// PRIVADA no tiene nada que hacer dentro de una aplicación de móvil. En SharedKernel.Server
/// tampoco: ese proyecto existe para lo que necesita un anfitrión web —HttpContext,
/// DataProtection, Redis, la tubería de MediatR— y firmar un JWT no necesita ninguna de esas
/// cosas; su propio .csproj pide comprobarlo antes de añadir nada.
///
/// Su sitio es Authn, que ya es la biblioteca de autenticación de la solución y ya firma con esta
/// misma llave RSA el reto del segundo factor (<see cref="ChallengeTokenService"/>). Con las dos
/// clases en el mismo proyecto, la invariante que las separa —el reto lleva la audiencia derivada
/// de <c>ChallengeAudience</c> y el token de acceso lleva la de verdad, que es lo único que impide
/// que un reto valga como sesión— se lee de un vistazo en vez de estar repartida entre dos
/// ensamblados.
///
/// Authn no referencia ASP.NET Core, así que traerlo aquí no toca las pruebas de invariante de
/// ClientCore ni de SharedComponents.
/// </remarks>
public sealed class JwtService : IJwtService
{
    private readonly IConfiguration _config;
    private readonly string         _issuer;
    private readonly string         _audience;
    private readonly RsaSecurityKey _signingKey;

    public JwtService(IConfiguration config)
    {
        _config   = config;
        _issuer   = config["Jwt:Issuer"]   ?? throw new InvalidOperationException("Jwt:Issuer not configured.");
        _audience = config["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience not configured.");

        var privateKeyBase64 = JwtKeyGuard.ValidatePrivateKey(config["Jwt:PrivateKeyBase64"]);

        var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
        _signingKey = new RsaSecurityKey(rsa);
    }

    public TimeSpan AccessTokenExpiry  => TimeSpan.FromMinutes(_config.GetValue("Jwt:AccessTokenExpiryMinutes", 15));
    public TimeSpan RefreshTokenExpiry => TimeSpan.FromDays(_config.GetValue("Jwt:RefreshTokenExpiryDays", 30));

    public string GenerateAccessToken(
        string userId,
        string memberId,
        string email,
        IEnumerable<string> roles,
        bool isImpersonating = false,
        string? impersonatedBy = null,
        string? defaultLanguage = null,
        bool impersonationReadOnly = false)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new("memberId",      memberId),
            new("impersonating", isImpersonating.ToString().ToLower()),
        };

        if (!string.IsNullOrEmpty(impersonatedBy))
            claims.Add(new Claim("impersonatedBy", impersonatedBy));

        // La restricción de solo lectura va en el TOKEN y no en la respuesta. Antes se calculaba
        // en StartImpersonationHandler y se devolvía en el cuerpo como dato informativo: el token
        // salía con los roles completos del miembro y dos horas de vida, así que "solo lectura"
        // dependía de que la interfaz quisiera honrarlo. Aquí dentro va firmado y lo aplica el
        // servidor que recibe la petición. Ver ImpersonationScope.
        //
        // El claim solo se escribe cuando de verdad restringe: un `false` explícito en todos los
        // demás tokens no añadiría nada y sí invitaría a leerlo como "restricción evaluada", que
        // es distinto de "no es un token restringido".
        if (impersonationReadOnly)
            claims.Add(new Claim(
                ImpersonationScope.ReadOnlyClaim, ImpersonationScope.ReadOnlyValue));

        // Idioma preferido — lo propaga el manejador de la cookie de sesión de BizCenterWeb para
        // que un inicio de sesión en un dispositivo nuevo caiga en el idioma del usuario sin una
        // vuelta extra a /profile.
        if (!string.IsNullOrEmpty(defaultLanguage))
            claims.Add(new Claim("default_language", defaultLanguage));

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);
        var expiry      = DateTime.UtcNow.Add(AccessTokenExpiry);

        var token = new JwtSecurityToken(
            issuer:             _issuer,
            audience:           _audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            expiry,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
