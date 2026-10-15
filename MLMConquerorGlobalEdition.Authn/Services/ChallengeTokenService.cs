using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Authn.Services;

/// <inheritdoc cref="IChallengeTokenService"/>
public sealed class ChallengeTokenService : IChallengeTokenService
{
    // El mismo nombre que rechazan los anfitriones como segundo cinturón: una sola definición
    // para que el claim que se escribe aquí y el que allí se rechaza no puedan divergir.
    private const string PurposeClaim      = ChallengeAudience.PurposeClaim;
    private const string CodeHashClaim     = "code_hash";
    private const string ChannelClaim      = "channel";
    private const string OperationKeyClaim = "operation_key";

    private const string LoginPurpose      = "login";
    private const string EnrollmentPurpose = "enrollment";
    private const string StepUpPrefix      = "step_up:";

    private const string InvalidChallenge = "INVALID_CHALLENGE";
    private const string CodeExpired      = "CODE_EXPIRED";

    /// <summary>
    /// Etiquetas HKDF (el <c>info</c>) de las claves que se derivan de la llave privada. Fijas y
    /// versionadas: cambiarlas cambia las claves, así que solo se tocan a propósito. Una por uso,
    /// para que la huella del reto y la marca antirreplay del TOTP no compartan clave y lo
    /// calculado para una no pueda presentarse como la otra.
    /// </summary>
    internal const string CodeMacInfo    = "mlmconqueror/2fa-code-mac/v1";
    internal const string TotpReplayInfo = "mlmconqueror/2fa-totp-replay/v1";

    private readonly RsaSecurityKey    _signingKey;
    private readonly RsaSecurityKey    _validationKey;
    private readonly string            _issuer;
    private readonly string            _audience;
    private readonly IDateTimeProvider _dateTime;
    private readonly byte[]            _codeMacKey;
    private readonly byte[]            _totpReplayKey;

    public ChallengeTokenService(IConfiguration config, IDateTimeProvider dateTime)
    {
        _dateTime = dateTime;
        _issuer   = config["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer not configured.");

        // Audiencia propia para los retos, derivada de la de acceso. Es lo único que impide que
        // un reto —emitido en el login antes de verificar ningún código, y con `sub` dentro—
        // valga como token de acceso: los anfitriones validan `Jwt:Audience` y este token no la
        // lleva, así que lo rechazan sin tener que comprobar nada más. Ver ChallengeAudience.
        var accessAudience = config["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience not configured.");
        _audience = ChallengeAudience.For(accessAudience);

        var privateKeyBase64 = JwtKeyGuard.ValidatePrivateKey(config["Jwt:PrivateKeyBase64"]);
        var rsaPrivate = RSA.Create();
        rsaPrivate.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
        _signingKey = new RsaSecurityKey(rsaPrivate);

        // La clave de la huella del código se DERIVA de la llave privada en vez de ser un secreto
        // nuevo: no hay nada más que configurar ni repartir, y la tienen exactamente los
        // anfitriones que ya pueden firmar retos —este servicio no arranca sin ella—, que son los
        // únicos que necesitan emitirlos y verificarlos. Un anfitrión que solo valida tokens con
        // la llave pública no puede calcular huellas, y no le hace falta.
        //
        // No se usa la llave RSA directamente como clave HMAC: mezclar usos de una misma clave
        // invita a interacciones que nadie ha analizado. HKDF-SHA256 con una etiqueta propia saca
        // de ella una clave independiente; quien tenga la huella no aprende nada de la llave de
        // firma ni al revés.
        //
        // La entrada de HKDF es la llave RE-EXPORTADA en PKCS#8, no los bytes tal cual vienen en la
        // configuración. La misma llave admite más de una codificación —ImportPkcs8PrivateKey
        // ignora lo que sobre detrás de la estructura, y otra herramienta puede escribir atributos
        // o la versión 1 del formato—: con los bytes crudos, dos anfitriones con la MISMA llave
        // escrita de dos maneras derivarían claves distintas, firmarían retos que el otro acepta y
        // rechazarían como CODE_INVALID cualquier código bueno. La exportación es la forma única
        // que da .NET para esa llave.
        //
        // Consecuencia al rotar la llave JWT: las huellas de los retos en vuelo dejan de cuadrar y
        // esos retos fallan con CODE_INVALID. Con 5 minutos de vida es asumible: el usuario pide
        // otro código.
        var canonicalPrivateKey = rsaPrivate.ExportPkcs8PrivateKey();
        try
        {
            _codeMacKey    = DeriveKey(canonicalPrivateKey, CodeMacInfo);
            _totpReplayKey = DeriveKey(canonicalPrivateKey, TotpReplayInfo);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonicalPrivateKey);
        }

        var publicKeyBase64 = JwtKeyGuard.ValidatePublicKey(config["Jwt:PublicKeyBase64"]);
        var rsaPublic = RSA.Create();
        rsaPublic.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
        _validationKey = new RsaSecurityKey(rsaPublic);

        ChallengeLifetime = TimeSpan.FromMinutes(
            ReadMinutes(config, "Auth:TwoFactor:ChallengeLifetimeMinutes", 5));
        ResendGraceWindow = TimeSpan.FromMinutes(
            ReadMinutes(config, "Auth:TwoFactor:ResendGraceWindowMinutes", 30));
    }

    public TimeSpan ChallengeLifetime { get; }
    public TimeSpan ResendGraceWindow { get; }

    public string GenerateCode()
    {
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        var num = BitConverter.ToUInt32(bytes) % 1_000_000;
        return num.ToString("D6");
    }

    /// <summary>
    /// Huella del código para un reto concreto: HMAC-SHA256(clave, jti + ":" + código), en base64.
    /// El jti entra en el mensaje para que la huella de un reto no sirva en otro: el mismo código
    /// enviado dos veces da dos huellas distintas, y copiar la de A en B no prueba nada.
    /// </summary>
    private string CodeMac(string jti, string code) =>
        Convert.ToBase64String(HMACSHA256.HashData(_codeMacKey, Encoding.UTF8.GetBytes(jti + ":" + code)));

    /// <inheritdoc/>
    public bool VerifyCode(ChallengeClaims claims, string code)
    {
        if (string.IsNullOrEmpty(claims.CodeHash) || string.IsNullOrEmpty(code))
            return false;

        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(claims.CodeHash);
        }
        catch (FormatException)
        {
            return false;
        }

        // Se recalcula con el jti del reto PRESENTADO, el que viene firmado junto a la huella.
        // Comparación en tiempo constante: con == el tiempo de respuesta filtraría cuántos bytes
        // iniciales coinciden.
        var actual = HMACSHA256.HashData(_codeMacKey, Encoding.UTF8.GetBytes(claims.Jti + ":" + code));
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    /// <inheritdoc/>
    public string TotpReplayTag(string userId, string code) =>
        // Base64 URL-safe: va dentro de una clave de caché, y '/' o '+' ahí solo estorban.
        Base64UrlEncoder.Encode(HMACSHA256.HashData(_totpReplayKey, Encoding.UTF8.GetBytes(userId + ":" + code)));

    private static byte[] DeriveKey(byte[] privateKeyBytes, string info) =>
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm:          privateKeyBytes,
            outputLength: 32,
            salt:         [],
            info:         Encoding.UTF8.GetBytes(info));

    public string Issue(
        string           userId,
        string           email,
        TwoFactorPurpose purpose,
        TwoFactorChannel channel,
        string?          code,
        string?          operationKey = null)
    {
        if (purpose == TwoFactorPurpose.StepUp && string.IsNullOrWhiteSpace(operationKey))
            throw new ArgumentException(
                "Un challenge de step-up necesita la operación que autoriza; sin ella el código " +
                "valdría para cualquier operación.", nameof(operationKey));

        // Correo y SMS llevan un código nuestro, así que el challenge tiene que llevar su huella:
        // sin ella no habría nada contra qué comparar lo que devuelva el usuario. Authenticator no,
        // porque el código lo genera su aplicación y lo verifica Identity.
        if (channel != TwoFactorChannel.Authenticator && string.IsNullOrWhiteSpace(code))
            throw new ArgumentException(
                $"El canal {channel} exige el código enviado.", nameof(code));

        var now    = _dateTime.UtcNow;
        var expiry = now.Add(ChallengeLifetime);
        var jti    = Guid.NewGuid().ToString();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti,   jti),
            new(PurposeClaim,                  FormatPurpose(purpose, operationKey)),
            new(ChannelClaim,                  channel.ToString()),
        };

        if (purpose == TwoFactorPurpose.StepUp)
            claims.Add(new Claim(OperationKeyClaim, operationKey!));

        if (channel != TwoFactorChannel.Authenticator)
            claims.Add(new Claim(CodeHashClaim, CodeMac(jti, code!)));

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            issuer:             _issuer,
            audience:           _audience,
            claims:             claims,
            notBefore:          now,
            expires:            expiry,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public Result<ChallengeClaims> Validate(
        string           challengeToken,
        TwoFactorPurpose expectedPurpose,
        string?          expectedOperationKey = null,
        bool             allowExpired = false)
    {
        if (string.IsNullOrWhiteSpace(challengeToken))
            return Fail("Challenge token missing.");

        // Un step-up sin operación esperada no se puede comprobar contra nada: rechazar aquí
        // evita que un descuido en el endpoint convierta el propósito en un sello vacío.
        if (expectedPurpose == TwoFactorPurpose.StepUp && string.IsNullOrWhiteSpace(expectedOperationKey))
            return Fail("Step-up validation requires the expected operation key.");

        // MapInboundClaims = false: sin esto el handler renombra "sub" y "email" a los URIs de
        // WS-Federation según un mapa estático global, y esta librería leería null en servicios
        // que no lo hayan limpiado en su arranque.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = !allowExpired,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = _issuer,
            ValidAudience            = _audience,
            IssuerSigningKey         = _validationKey,
            ClockSkew                = TimeSpan.Zero,
            // El handler mediría la vigencia contra el reloj de la máquina, ignorando el
            // IDateTimeProvider inyectado. Se valida con el mismo reloj que emite y que mide la
            // ventana de reenvío, para que las tres decisiones no puedan discrepar.
            // Se deja nulo con allowExpired: el delegado, si está puesto, corre aunque
            // ValidateLifetime sea false, y volvería a rechazar lo que aquí se quiere admitir.
            LifetimeValidator        = allowExpired ? null : ValidateLifetimeAgainstInjectedClock
        };

        ClaimsPrincipal principal;
        SecurityToken   validatedToken;
        try
        {
            principal = handler.ValidateToken(challengeToken, parameters, out validatedToken);
        }
        catch (SecurityTokenExpiredException)
        {
            return Result<ChallengeClaims>.Failure(CodeExpired, "The verification code has expired.");
        }
        catch
        {
            return Fail("Challenge token is invalid.");
        }

        // El corazón de la separación: el propósito grabado tiene que ser exactamente el que
        // espera este endpoint, operación incluida cuando es step-up. Un token de login
        // presentado a un endpoint de step-up (o de una operación a otra) muere aquí.
        var purposeClaim = principal.FindFirst(PurposeClaim)?.Value;
        if (!string.Equals(purposeClaim, FormatPurpose(expectedPurpose, expectedOperationKey), StringComparison.Ordinal))
            return Fail("Challenge token is invalid.");

        var jwt = (JwtSecurityToken)validatedToken;
        var iat = jwt.ValidFrom;
        var exp = jwt.ValidTo;

        if (allowExpired && _dateTime.UtcNow - iat > ResendGraceWindow)
            return Result<ChallengeClaims>.Failure(
                CodeExpired, "Challenge is too old to resend; please log in again.");

        var jti    = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var email  = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

        if (string.IsNullOrEmpty(jti) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
            return Fail("Challenge token is malformed.");

        var channelClaim = principal.FindFirst(ChannelClaim)?.Value;
        if (!Enum.TryParse<TwoFactorChannel>(channelClaim, ignoreCase: true, out var channel))
            return Fail("Challenge token is malformed.");

        var codeHash = principal.FindFirst(CodeHashClaim)?.Value;
        if (channel == TwoFactorChannel.Authenticator)
            codeHash = null;
        else if (string.IsNullOrEmpty(codeHash))
            return Fail("Challenge token is malformed.");

        // La operación ya viene comprobada dentro del propósito; el claim suelto tiene que decir
        // lo mismo. Si discrepan, el token no lo emitió este servicio tal como lo emite hoy.
        var operationKey = expectedPurpose == TwoFactorPurpose.StepUp
            ? principal.FindFirst(OperationKeyClaim)?.Value
            : null;

        if (expectedPurpose == TwoFactorPurpose.StepUp &&
            !string.Equals(operationKey, expectedOperationKey, StringComparison.Ordinal))
            return Fail("Challenge token is malformed.");

        return Result<ChallengeClaims>.Success(new ChallengeClaims(
            Jti:          jti,
            UserId:       userId,
            Email:        email,
            Purpose:      expectedPurpose,
            OperationKey: operationKey,
            Channel:      channel,
            CodeHash:     codeHash,
            IssuedAt:     iat,
            ExpiresAt:    exp));
    }

    /// <summary>
    /// Serializa el propósito tal como viaja en el claim. Para step-up la operación va pegada al
    /// propósito, de modo que la comparación de una sola cadena cubre las dos cosas y no hay
    /// forma de comprobar el propósito y olvidarse de la operación.
    /// </summary>
    private static string FormatPurpose(TwoFactorPurpose purpose, string? operationKey) => purpose switch
    {
        TwoFactorPurpose.Login      => LoginPurpose,
        TwoFactorPurpose.Enrollment => EnrollmentPurpose,
        TwoFactorPurpose.StepUp     => StepUpPrefix + operationKey,
        _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "Propósito de 2FA desconocido.")
    };


    /// <summary>
    /// Reemplaza la validación de vigencia del handler para medirla contra el reloj inyectado.
    /// Lanza <see cref="SecurityTokenExpiredException"/> en vez de devolver false para que el
    /// vencimiento se distinga de un token inválido y llegue al llamante como <c>CODE_EXPIRED</c>.
    /// </summary>
    private bool ValidateLifetimeAgainstInjectedClock(
        DateTime? notBefore, DateTime? expires, SecurityToken token, TokenValidationParameters parameters)
    {
        var now = _dateTime.UtcNow;

        if (expires is null)
            return false;

        if (now >= expires.Value)
            throw new SecurityTokenExpiredException($"Challenge expired at {expires.Value:O}.");

        return notBefore is null || now >= notBefore.Value;
    }

    private static Result<ChallengeClaims> Fail(string message) =>
        Result<ChallengeClaims>.Failure(InvalidChallenge, message);

    private static int ReadMinutes(IConfiguration config, string key, int fallback) =>
        int.TryParse(config[key], out var minutes) && minutes > 0 ? minutes : fallback;
}
