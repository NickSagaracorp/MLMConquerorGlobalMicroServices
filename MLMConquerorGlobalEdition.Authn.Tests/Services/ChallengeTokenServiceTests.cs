using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.Authn.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Authn.Tests.Services;

public class ChallengeTokenServiceTests
{
    private const string Issuer   = "MLMConqueror";
    private const string Audience = "MLMConquerorUsers";

    private static readonly DateTime FixedNow = new(2026, 01, 15, 10, 00, 00, DateTimeKind.Utc);

    /// <summary>Par RSA nuevo por llamada, igual que <c>JwtServiceTests.GeneratePrivateKeyBase64</c>.</summary>
    private static (string PrivateKeyBase64, string PublicKeyBase64) GenerateKeyPair()
    {
        using var rsa = RSA.Create(2048);
        return (Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()),
                Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()));
    }

    private static IConfiguration BuildConfig(
        (string PrivateKeyBase64, string PublicKeyBase64) keys,
        int challengeLifetimeMinutes = 5,
        int resendGraceWindowMinutes = 30)
    {
        var data = new Dictionary<string, string?>
        {
            ["Jwt:PrivateKeyBase64"]                        = keys.PrivateKeyBase64,
            ["Jwt:PublicKeyBase64"]                         = keys.PublicKeyBase64,
            ["Jwt:Issuer"]                                  = Issuer,
            ["Jwt:Audience"]                                = Audience,
            ["Auth:TwoFactor:ChallengeLifetimeMinutes"]     = challengeLifetimeMinutes.ToString(),
            ["Auth:TwoFactor:ResendGraceWindowMinutes"]     = resendGraceWindowMinutes.ToString()
        };
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    private static Mock<IDateTimeProvider> ClockAt(DateTime now)
    {
        var clock = new Mock<IDateTimeProvider>();
        MoveClockTo(clock, now);
        return clock;
    }

    /// <summary>
    /// Fija el reloj de prueba. Toca Now y UtcNow a la vez: el servicio lee UtcNow —los
    /// tiempos de un JWT son epoch UTC por especificación— y mover solo Now dejaría el
    /// token sin envejecer, haciendo que una prueba de expiración pasara por la razón
    /// equivocada. Now se fija al mismo valor para que leer el miembro equivocado se note
    /// como una fecha absurda y no como un desfase silencioso.
    /// </summary>
    private static void MoveClockTo(Mock<IDateTimeProvider> clock, DateTime now)
    {
        clock.Setup(c => c.Now).Returns(now);
        clock.Setup(c => c.UtcNow).Returns(now);
    }

    private static ChallengeTokenService BuildService(
        out Mock<IDateTimeProvider> clock,
        (string PrivateKeyBase64, string PublicKeyBase64)? keys = null,
        DateTime? now = null,
        int challengeLifetimeMinutes = 5,
        int resendGraceWindowMinutes = 30)
    {
        clock = ClockAt(now ?? FixedNow);
        return new ChallengeTokenService(
            BuildConfig(keys ?? GenerateKeyPair(), challengeLifetimeMinutes, resendGraceWindowMinutes),
            clock.Object);
    }

    // ── 1. round-trip ────────────────────────────────────────────────────────

    [Fact]
    public void Issue_ThenValidate_RoundTrips()
    {
        var service  = BuildService(out _);
        var code     = service.GenerateCode();

        var token = service.Issue(
            userId:  "user-001",
            email:   "test@test.com",
            purpose: TwoFactorPurpose.Login,
            channel: TwoFactorChannel.Email,
            code:    code);

        var result = service.Validate(token, TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        var claims = result.Value!;
        claims.Jti.Should().NotBeNullOrWhiteSpace();
        claims.UserId.Should().Be("user-001");
        claims.Email.Should().Be("test@test.com");
        claims.Purpose.Should().Be(TwoFactorPurpose.Login);
        claims.OperationKey.Should().BeNull();
        claims.Channel.Should().Be(TwoFactorChannel.Email);
        claims.CodeHash.Should().NotBeNullOrWhiteSpace();
        service.VerifyCode(claims, code).Should().BeTrue();
        claims.IssuedAt.Should().BeCloseTo(FixedNow, TimeSpan.FromSeconds(1));
        claims.ExpiresAt.Should().BeCloseTo(FixedNow.AddMinutes(5), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Issue_StepUp_ThenValidate_RoundTripsOperationKey()
    {
        var service = BuildService(out _);

        var token = service.Issue(
            "user-001", "test@test.com",
            TwoFactorPurpose.StepUp, TwoFactorChannel.Sms,
            "123456",
            operationKey: "PAYOUT_BATCH_RELEASE");

        var result = service.Validate(token, TwoFactorPurpose.StepUp, "PAYOUT_BATCH_RELEASE");

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.Purpose.Should().Be(TwoFactorPurpose.StepUp);
        result.Value!.OperationKey.Should().Be("PAYOUT_BATCH_RELEASE");
        result.Value!.Channel.Should().Be(TwoFactorChannel.Sms);
    }

    // ── 2. el propósito separa los usos ──────────────────────────────────────

    [Fact]
    public void Validate_WhenPurposeDiffers_Fails()
    {
        var service = BuildService(out _);

        // Un código pedido para iniciar sesión...
        var token = service.Issue(
            "user-001", "test@test.com",
            TwoFactorPurpose.Login, TwoFactorChannel.Email,
            "123456");

        // ...no autoriza una operación crítica.
        var result = service.Validate(token, TwoFactorPurpose.StepUp, "PAYOUT_BATCH_RELEASE");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CHALLENGE");
    }

    [Fact]
    public void Validate_WhenStepUpTokenPresentedForLogin_Fails()
    {
        var service = BuildService(out _);

        var token = service.Issue(
            "user-001", "test@test.com",
            TwoFactorPurpose.StepUp, TwoFactorChannel.Email,
            "123456",
            operationKey: "PAYOUT_BATCH_RELEASE");

        var result = service.Validate(token, TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CHALLENGE");
    }

    // ── 3. la operación separa los step-up entre sí ──────────────────────────

    [Fact]
    public void Validate_WhenOperationKeyDiffers_Fails()
    {
        var service = BuildService(out _);

        var token = service.Issue(
            "user-001", "test@test.com",
            TwoFactorPurpose.StepUp, TwoFactorChannel.Email,
            "123456",
            operationKey: "PAYOUT_BATCH_RELEASE");

        var result = service.Validate(token, TwoFactorPurpose.StepUp, "SYSTEM_USER_DELETE");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CHALLENGE");
    }

    [Fact]
    public void Validate_WhenStepUpExpectedWithoutOperationKey_Fails()
    {
        var service = BuildService(out _);

        var token = service.Issue(
            "user-001", "test@test.com",
            TwoFactorPurpose.StepUp, TwoFactorChannel.Email,
            "123456",
            operationKey: "PAYOUT_BATCH_RELEASE");

        var result = service.Validate(token, TwoFactorPurpose.StepUp);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CHALLENGE");
    }

    // ── 4. vigencia ──────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WhenExpired_ReturnsCodeExpired()
    {
        var service = BuildService(out var clock, challengeLifetimeMinutes: 5);

        var token = service.Issue(
            "user-001", "test@test.com",
            TwoFactorPurpose.Login, TwoFactorChannel.Email,
            "123456");

        MoveClockTo(clock, FixedNow.AddMinutes(6));

        var result = service.Validate(token, TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CODE_EXPIRED");
    }

    // ── 5 y 6. integridad de la firma ────────────────────────────────────────

    [Fact]
    public void Validate_WhenTamperedSignature_Fails()
    {
        var service = BuildService(out _);

        var token = service.Issue(
            "user-001", "test@test.com",
            TwoFactorPurpose.Login, TwoFactorChannel.Email,
            "123456");

        // Alterar un carácter de la firma invalida el token.
        var lastChar = token[^1];
        var tampered = token[..^1] + (lastChar == 'A' ? 'B' : 'A');

        var result = service.Validate(tampered, TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CHALLENGE");
    }

    [Fact]
    public void Validate_WhenIssuedByAnotherKey_Fails()
    {
        var attacker = BuildService(out _, keys: GenerateKeyPair());
        var victim   = BuildService(out _, keys: GenerateKeyPair());

        var token = attacker.Issue(
            "user-001", "test@test.com",
            TwoFactorPurpose.Login, TwoFactorChannel.Email,
            "123456");

        var result = victim.Validate(token, TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CHALLENGE");
    }

    // ── 7. TOTP no lleva code_hash ───────────────────────────────────────────

    [Fact]
    public void Issue_WhenChannelIsAuthenticator_HasNoCodeHash()
    {
        var service = BuildService(out _);

        var token = service.Issue(
            "user-001", "test@test.com",
            TwoFactorPurpose.Login, TwoFactorChannel.Authenticator,
            code: null);

        var result = service.Validate(token, TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.Channel.Should().Be(TwoFactorChannel.Authenticator);
        result.Value!.CodeHash.Should().BeNull();
    }

    // ── 8 y 9. ventana de gracia para el reenvío ─────────────────────────────

    [Fact]
    public void Validate_AllowExpired_WithinGraceWindow_Succeeds()
    {
        var service = BuildService(
            out var clock, challengeLifetimeMinutes: 5, resendGraceWindowMinutes: 30);

        var token = service.Issue(
            "user-001", "test@test.com",
            TwoFactorPurpose.Login, TwoFactorChannel.Email,
            "123456");

        // Vencido como código, pero todavía reenviable.
        MoveClockTo(clock, FixedNow.AddMinutes(20));

        var result = service.Validate(token, TwoFactorPurpose.Login, allowExpired: true);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.UserId.Should().Be("user-001");
    }

    [Fact]
    public void Validate_AllowExpired_BeyondGraceWindow_Fails()
    {
        var service = BuildService(
            out var clock, challengeLifetimeMinutes: 5, resendGraceWindowMinutes: 30);

        var token = service.Issue(
            "user-001", "test@test.com",
            TwoFactorPurpose.Login, TwoFactorChannel.Email,
            "123456");

        MoveClockTo(clock, FixedNow.AddMinutes(31));

        var result = service.Validate(token, TwoFactorPurpose.Login, allowExpired: true);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CODE_EXPIRED");
    }

    [Fact]
    public void Validate_AllowExpired_StillRejectsMismatchedPurpose()
    {
        var service = BuildService(out var clock, challengeLifetimeMinutes: 5);

        var token = service.Issue(
            "user-001", "test@test.com",
            TwoFactorPurpose.Login, TwoFactorChannel.Email,
            "123456");

        MoveClockTo(clock, FixedNow.AddMinutes(6));

        var result = service.Validate(
            token, TwoFactorPurpose.StepUp, "PAYOUT_BATCH_RELEASE", allowExpired: true);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CHALLENGE");
    }

    // ── código y hash ────────────────────────────────────────────────────────

    [Fact]
    public void GenerateCode_ReturnsSixDigits()
    {
        var service = BuildService(out _);

        for (var i = 0; i < 50; i++)
        {
            var code = service.GenerateCode();
            code.Should().HaveLength(6);
            code.Should().MatchRegex("^[0-9]{6}$");
        }
    }

    // ── la huella no revela el código ────────────────────────────────────────

    /// <summary>Lo que cualquiera que tenga el reto puede leer: el payload es base64, no está cifrado.</summary>
    private static string CodeHashClaimOf(string token) =>
        new JwtSecurityTokenHandler { MapInboundClaims = false }
            .ReadJwtToken(token).Claims.Single(c => c.Type == "code_hash").Value;

    private static string Sha256Base64(string code) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    [Fact]
    public void La_huella_del_reto_no_se_invierte_fuera_de_linea_sin_la_clave()
    {
        // El ataque que esto cierra: quien tiene el reto (el que solo sabe la contraseña, o quien
        // robó un token de acceso y pidió un step-up) decodifica el payload y prueba el millón de
        // códigos contra la huella, sin pasar por el límite de intentos. Aquí se hace eso mismo,
        // exhaustivo, con SHA-256 sin clave, y no puede encontrar nada.
        var service = BuildService(out _);
        var code    = service.GenerateCode();
        var token   = service.Issue(
            "user-001", "test@test.com", TwoFactorPurpose.Login, TwoFactorChannel.Email, code);

        var claim  = CodeHashClaimOf(token);
        var huella = Convert.FromBase64String(claim);

        Sha256Base64(code).Should().NotBe(claim);

        var mensaje = new byte[6];
        var digest  = new byte[32];
        string? encontrado = null;
        for (var n = 0; n < 1_000_000 && encontrado is null; n++)
        {
            n.TryFormat(mensaje, out _, "D6");
            SHA256.HashData(mensaje, digest);
            if (digest.AsSpan().SequenceEqual(huella))
                encontrado = n.ToString("D6");
        }

        encontrado.Should().BeNull("una huella sin clave se invertiría probando los 10^6 códigos");

        // El jti va en claro en el mismo payload, así que el atacante también puede probar la
        // construcción "con sal pero sin clave": SHA-256(jti + ":" + código). Atar la huella al jti
        // sin una clave detrás no defendería nada —la sal es pública—, y esto lo comprueba
        // recorriendo otra vez el millón entero.
        var jti     = service.Validate(token, TwoFactorPurpose.Login).Value!.Jti;
        var prefijo = Encoding.UTF8.GetBytes(jti + ":");
        var salado  = new byte[prefijo.Length + 6];
        prefijo.CopyTo(salado, 0);

        SHA256.HashData(Encoding.UTF8.GetBytes(jti + ":" + code)).Should().NotEqual(huella);

        for (var n = 0; n < 1_000_000 && encontrado is null; n++)
        {
            n.TryFormat(salado.AsSpan(prefijo.Length), out _, "D6");
            SHA256.HashData(salado, digest);
            if (digest.AsSpan().SequenceEqual(huella))
                encontrado = n.ToString("D6");
        }

        encontrado.Should().BeNull("SHA-256(jti:código) usa una sal pública y se invertiría igual de rápido");
    }

    [Fact]
    public void La_huella_es_un_HMAC_con_clave_derivada_de_la_llave_privada_y_atada_al_jti()
    {
        // Fija la construcción documentada: HKDF-SHA256 sobre la llave privada re-exportada en
        // PKCS#8 (la forma canónica) con la etiqueta "mlmconqueror/2fa-code-mac/v1", y
        // HMAC-SHA256(clave, jti + ":" + código). Si alguien cambia la etiqueta, el mensaje o la
        // entrada de HKDF sin querer, esto lo dice.
        //
        // La llave se configura con bytes de más detrás de la estructura PKCS#8: la importación los
        // ignora, así que es la misma llave con otra codificación. Con los bytes crudos como entrada
        // la clave derivada cambiaría; con la exportación canónica, no.
        var keys      = GenerateKeyPair();
        var canonica  = Convert.FromBase64String(keys.PrivateKeyBase64);
        var conRelleno = Convert.ToBase64String([.. canonica, 0x00, 0x00, 0x00, 0x00]);

        var service = BuildService(out _, keys: (conRelleno, keys.PublicKeyBase64));
        var token   = service.Issue(
            "user-001", "test@test.com", TwoFactorPurpose.Login, TwoFactorChannel.Email, "123456");

        var jti   = service.Validate(token, TwoFactorPurpose.Login).Value!.Jti;
        var clave = HKDF.DeriveKey(
            HashAlgorithmName.SHA256, canonica, 32,
            salt: [], info: Encoding.UTF8.GetBytes("mlmconqueror/2fa-code-mac/v1"));
        var esperado = Convert.ToBase64String(
            HMACSHA256.HashData(clave, Encoding.UTF8.GetBytes(jti + ":123456")));

        CodeHashClaimOf(token).Should().Be(esperado);
    }

    [Fact]
    public void Dos_codificaciones_de_la_misma_llave_verifican_los_retos_de_la_otra()
    {
        // Dos anfitriones con la misma llave escrita de dos maneras: lo que uno emite, el otro lo
        // tiene que verificar. Con HKDF sobre los bytes crudos, el código bueno salía CODE_INVALID.
        var keys       = GenerateKeyPair();
        var conRelleno = Convert.ToBase64String([.. Convert.FromBase64String(keys.PrivateKeyBase64), 0x30, 0x00]);

        var emisor    = BuildService(out _, keys: keys);
        var verificador = BuildService(out _, keys: (conRelleno, keys.PublicKeyBase64));

        var token  = emisor.Issue("user-001", "test@test.com", TwoFactorPurpose.Login, TwoFactorChannel.Email, "123456");
        var claims = verificador.Validate(token, TwoFactorPurpose.Login).Value!;

        verificador.VerifyCode(claims, "123456").Should().BeTrue();
        verificador.TotpReplayTag("user-001", "123456").Should().Be(emisor.TotpReplayTag("user-001", "123456"));
    }

    [Fact]
    public void El_mismo_codigo_en_dos_retos_da_dos_huellas_distintas()
    {
        var service = BuildService(out _);

        var a = service.Issue("user-001", "test@test.com", TwoFactorPurpose.Login, TwoFactorChannel.Email, "123456");
        var b = service.Issue("user-001", "test@test.com", TwoFactorPurpose.Login, TwoFactorChannel.Email, "123456");

        CodeHashClaimOf(a).Should().NotBe(CodeHashClaimOf(b));
    }

    [Fact]
    public void Otra_llave_privada_no_verifica_la_huella()
    {
        // La clave sale de la llave privada: quien no la tiene no puede calcular ni comprobar
        // huellas, y dos despliegues con llaves distintas no se aceptan las suyas.
        var uno    = BuildService(out _);
        var otro   = BuildService(out _);
        var token  = uno.Issue("user-001", "test@test.com", TwoFactorPurpose.Login, TwoFactorChannel.Email, "123456");
        var claims = uno.Validate(token, TwoFactorPurpose.Login).Value!;

        uno.VerifyCode(claims, "123456").Should().BeTrue();
        otro.VerifyCode(claims, "123456").Should().BeFalse();
    }

    [Fact]
    public void VerifyCode_AceptaElCodigoBuenoYRechazaLosDemas()
    {
        var service = BuildService(out _);
        var token   = service.Issue(
            "user-001", "test@test.com", TwoFactorPurpose.StepUp, TwoFactorChannel.Sms, "123456",
            operationKey: "PAYOUT_WALLET_CHANGE");
        var claims  = service.Validate(token, TwoFactorPurpose.StepUp, "PAYOUT_WALLET_CHANGE").Value!;

        service.VerifyCode(claims, "123456").Should().BeTrue();
        service.VerifyCode(claims, "654321").Should().BeFalse();
        service.VerifyCode(claims, "").Should().BeFalse();
    }

    [Fact]
    public void Una_huella_copiada_de_otro_reto_no_se_canjea()
    {
        // B se emite para otro código; se le pega la huella de A, cuyo código sí conoce quien
        // ataca. La verificación recalcula con el jti del reto presentado (B), así que la huella
        // de A no cuadra. Falsificar B entero ni siquiera llega aquí: la firma no lo deja pasar.
        var service = BuildService(out _);
        var a = service.Validate(
            service.Issue("user-001", "test@test.com", TwoFactorPurpose.Login, TwoFactorChannel.Email, "111111"),
            TwoFactorPurpose.Login).Value!;
        var b = service.Validate(
            service.Issue("user-001", "test@test.com", TwoFactorPurpose.Login, TwoFactorChannel.Email, "222222"),
            TwoFactorPurpose.Login).Value!;

        var bConHuellaDeA = b with { CodeHash = a.CodeHash };

        service.VerifyCode(a, "111111").Should().BeTrue();
        service.VerifyCode(bConHuellaDeA, "111111").Should().BeFalse();
    }

    [Fact]
    public void VerifyCode_SinHuellaOMalFormada_DevuelveFalso()
    {
        var service = BuildService(out _);
        var claims  = service.Validate(
            service.Issue("user-001", "test@test.com", TwoFactorPurpose.Login, TwoFactorChannel.Authenticator, code: null),
            TwoFactorPurpose.Login).Value!;

        service.VerifyCode(claims, "123456").Should().BeFalse();
        service.VerifyCode(claims with { CodeHash = "no-es-base64!" }, "123456").Should().BeFalse();
    }

    [Fact]
    public void TotpReplayTag_EsEstablePorUsuarioYCodigo_YNoEsUnHashSinClave()
    {
        var service = BuildService(out _);

        var tag = service.TotpReplayTag("user-001", "123456");

        tag.Should().Be(service.TotpReplayTag("user-001", "123456"));
        tag.Should().NotBe(service.TotpReplayTag("user-001", "654321"));
        tag.Should().NotBe(service.TotpReplayTag("user-002", "123456"));
        tag.Should().NotBe(BuildService(out _).TotpReplayTag("user-001", "123456"));
        Base64UrlEncoder.DecodeBytes(tag).Should().NotEqual(SHA256.HashData(Encoding.UTF8.GetBytes("123456")));
        Base64UrlEncoder.DecodeBytes(tag).Should().NotEqual(SHA256.HashData(Encoding.UTF8.GetBytes("user-001:123456")));
    }

    [Fact]
    public void Issue_SinCodigoParaCorreo_Lanza()
    {
        var service = BuildService(out _);

        Action act = () => service.Issue(
            "user-001", "test@test.com", TwoFactorPurpose.Login, TwoFactorChannel.Email, code: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validate_WhenTokenIsEmpty_Fails()
    {
        var service = BuildService(out _);

        var result = service.Validate("", TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CHALLENGE");
    }

    // ── configuración ────────────────────────────────────────────────────────

    [Fact]
    public void Lifetimes_DefaultTo5And30Minutes()
    {
        var keys = GenerateKeyPair();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:PrivateKeyBase64"] = keys.PrivateKeyBase64,
                ["Jwt:PublicKeyBase64"]  = keys.PublicKeyBase64,
                ["Jwt:Issuer"]           = Issuer,
                ["Jwt:Audience"]         = Audience
            })
            .Build();

        var service = new ChallengeTokenService(config, ClockAt(FixedNow).Object);

        service.ChallengeLifetime.Should().Be(TimeSpan.FromMinutes(5));
        service.ResendGraceWindow.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void Constructor_WhenPrivateKeyMissing_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"]   = Issuer,
                ["Jwt:Audience"] = Audience
            })
            .Build();

        Action act = () => _ = new ChallengeTokenService(config, ClockAt(FixedNow).Object);

        act.Should().Throw<InvalidOperationException>().WithMessage("*PrivateKeyBase64*");
    }
}
