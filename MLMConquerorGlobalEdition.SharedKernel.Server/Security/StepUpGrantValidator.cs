using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.SharedKernel.Server.Security;

/// <summary>
/// Valida un permiso de step-up con la llave PÚBLICA. Cualquier servicio que valide tokens de
/// acceso puede validar esto sin tener con qué firmarlo.
/// </summary>
/// <remarks>
/// No comprueba el uso único: eso necesita estado y lo hace el filtro con la caché. Aquí solo
/// firma, emisor, audiencia, vigencia, propósito, usuario y operación.
/// </remarks>
public sealed class StepUpGrantValidator
{
    /// <summary>
    /// Margen que se perdona SOLO sobre <c>nbf</c>. El permiso lo firma SignupAPI y lo comprueba
    /// BizCenter, que son máquinas distintas con relojes distintos: si el de BizCenter va unos
    /// segundos por detrás, un permiso recién emitido aún "no es válido" y la persona, que acaba de
    /// teclear su código bien, recibe un rechazo que no se explica. Treinta segundos cubren una
    /// deriva normal con NTP sin abrir nada: el permiso sigue durando lo que dura (<c>exp</c>).
    /// </summary>
    public static readonly TimeSpan NotBeforeSkew = TimeSpan.FromSeconds(30);

    private readonly RsaSecurityKey    _validationKey;
    private readonly string            _issuer;
    private readonly string            _audience;
    private readonly IDateTimeProvider _dateTime;

    public StepUpGrantValidator(IConfiguration config, IDateTimeProvider dateTime)
    {
        _dateTime = dateTime;
        _issuer   = config["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer not configured.");
        var accessAudience = config["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience not configured.");
        _audience = StepUpGrant.AudienceFor(accessAudience);

        var publicKeyBase64 = JwtKeyGuard.ValidatePublicKey(config["Jwt:PublicKeyBase64"]);
        var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
        _validationKey = new RsaSecurityKey(rsa);
    }

    /// <returns>El jti del permiso, para que quien llama lo marque como consumido.</returns>
    public Result<string> Validate(string token, string expectedUserId, string expectedOperationKey)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(expectedUserId))
            return Invalid();

        // Una operación fuera del catálogo solo puede venir de un atributo mal escrito. Mejor
        // cerrar que comparar contra una clave que nadie emite y confiar en que nunca coincida.
        if (string.IsNullOrWhiteSpace(expectedOperationKey) || !StepUpOperations.IsKnown(expectedOperationKey))
            return Invalid();

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = _issuer,
            ValidAudience            = _audience,
            IssuerSigningKey         = _validationKey,
            ClockSkew                = TimeSpan.Zero,
            // Mismo reloj que firma: ver ChallengeTokenService.ValidateLifetimeAgainstInjectedClock.
            // La tolerancia es asimétrica a propósito: nbf perdona NotBeforeSkew (deriva entre
            // máquinas) y exp NO perdona nada, porque alargar la vida de un permiso de un solo uso
            // es lo único que aquí le sirve a un atacante.
            LifetimeValidator        = (nbf, exp, _, _) =>
            {
                var now = _dateTime.UtcNow;
                return exp is not null && now < exp.Value && (nbf is null || now >= nbf.Value - NotBeforeSkew);
            }
        };

        System.Security.Claims.ClaimsPrincipal principal;
        try { principal = handler.ValidateToken(token, parameters, out _); }
        catch { return Invalid(); }

        var purpose = principal.FindFirst(ChallengeAudience.PurposeClaim)?.Value;
        var op      = principal.FindFirst(StepUpGrant.OperationClaim)?.Value;
        var sub     = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var jti     = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

        if (!string.Equals(purpose, StepUpGrant.PurposeFor(expectedOperationKey), StringComparison.Ordinal) ||
            !string.Equals(op, expectedOperationKey, StringComparison.Ordinal) ||
            !string.Equals(sub, expectedUserId, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(jti))
            return Invalid();

        return Result<string>.Success(jti);
    }

    private static Result<string> Invalid() =>
        Result<string>.Failure(StepUpGrant.InvalidErrorCode, "El permiso de step-up no es válido.");
}
