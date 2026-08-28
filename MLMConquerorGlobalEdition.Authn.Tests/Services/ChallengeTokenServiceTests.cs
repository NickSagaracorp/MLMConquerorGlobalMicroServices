using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
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
        clock.Setup(c => c.Now).Returns(now);
        return clock;
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
        var codeHash = service.HashCode(code);

        var token = service.Issue(
            userId:  "user-001",
            email:   "test@test.com",
            purpose: TwoFactorPurpose.Login,
            channel: TwoFactorChannel.Email,
            codeHash: codeHash);

        var result = service.Validate(token, TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        var claims = result.Value!;
        claims.Jti.Should().NotBeNullOrWhiteSpace();
        claims.UserId.Should().Be("user-001");
        claims.Email.Should().Be("test@test.com");
        claims.Purpose.Should().Be(TwoFactorPurpose.Login);
        claims.OperationKey.Should().BeNull();
        claims.Channel.Should().Be(TwoFactorChannel.Email);
        claims.CodeHash.Should().Be(codeHash);
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
            service.HashCode("123456"),
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
            service.HashCode("123456"));

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
            service.HashCode("123456"),
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
            service.HashCode("123456"),
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
            service.HashCode("123456"),
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
            service.HashCode("123456"));

        clock.Setup(c => c.Now).Returns(FixedNow.AddMinutes(6));

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
            service.HashCode("123456"));

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
            attacker.HashCode("123456"));

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
            codeHash: null);

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
            service.HashCode("123456"));

        // Vencido como código, pero todavía reenviable.
        clock.Setup(c => c.Now).Returns(FixedNow.AddMinutes(20));

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
            service.HashCode("123456"));

        clock.Setup(c => c.Now).Returns(FixedNow.AddMinutes(31));

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
            service.HashCode("123456"));

        clock.Setup(c => c.Now).Returns(FixedNow.AddMinutes(6));

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

    [Fact]
    public void HashCode_IsDeterministicAndDiffersPerCode()
    {
        var service = BuildService(out _);

        service.HashCode("123456").Should().Be(service.HashCode("123456"));
        service.HashCode("123456").Should().NotBe(service.HashCode("654321"));
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
