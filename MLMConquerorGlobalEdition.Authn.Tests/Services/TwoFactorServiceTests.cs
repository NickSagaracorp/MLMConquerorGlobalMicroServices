using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Authn.Services;
using MLMConquerorGlobalEdition.Authn.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Authn.Tests.Services;

public class TwoFactorServiceTests
{
    private const string UserId        = "user-001";
    private const string Email         = "usuario@dominio.com";
    private const string Phone         = "+14155552671";
    private const string EncryptedPhone = "ENC:whatever";
    private const string Code          = "123456";
    private const string Token         = "challenge-token";

    private static readonly DateTime FixedNow = new(2026, 01, 15, 10, 00, 00, DateTimeKind.Utc);

    private readonly Mock<IChallengeTokenService>       _tokens      = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManager = UserManagerHelper.Create();
    private readonly Mock<IEmailService>                _email       = new();
    private readonly Mock<ISmsService>                  _sms         = new();
    private readonly Mock<IEncryptionService>           _encryption  = new();
    private readonly Mock<ICacheService>                _cache       = new();
    private readonly Mock<IDateTimeProvider>            _clock       = new();

    /// <summary>Caché de mentira: un diccionario. Los límites y el antirreplay solo tienen
    /// sentido si lo escrito en una llamada se lee en la siguiente.</summary>
    private readonly Dictionary<string, object> _store = new();

    public TwoFactorServiceTests()
    {
        MoveClockTo(FixedNow);

        _tokens.SetupGet(t => t.ChallengeLifetime).Returns(TimeSpan.FromMinutes(5));
        _tokens.Setup(t => t.GenerateCode()).Returns(Code);
        _tokens.Setup(t => t.HashCode(It.IsAny<string>())).Returns<string>(HashOf);
        _tokens.Setup(t => t.Issue(
                   It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TwoFactorPurpose>(),
                   It.IsAny<TwoFactorChannel>(), It.IsAny<string?>(), It.IsAny<string?>()))
               .Returns(Token);

        _encryption.Setup(e => e.Decrypt(It.IsAny<string>())).Returns(Phone);

        WireCache<string>();
        WireCache<TwoFactorIssueWindow>();
    }

    // ── andamiaje ────────────────────────────────────────────────────────────

    /// <summary>
    /// Fija el reloj de prueba. Now y UtcNow al mismo valor: el servicio usa Now para el
    /// negocio y UtcNow solo para restar contra el <c>exp</c> del challenge, que viene del
    /// token; fijar los dos evita que una prueba pase por el desfase equivocado.
    /// </summary>
    private void MoveClockTo(DateTime now)
    {
        _clock.Setup(c => c.Now).Returns(now);
        _clock.Setup(c => c.UtcNow).Returns(now);
    }

    private void WireCache<T>() where T : class
    {
        _cache.Setup(c => c.GetAsync<T>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((string key, CancellationToken _) =>
                  _store.TryGetValue(key, out var value) ? (T?)value : null);

        _cache.Setup(c => c.SetAsync(
                  It.IsAny<string>(), It.IsAny<T>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
              .Callback((string key, T value, TimeSpan _, CancellationToken _) => _store[key] = value)
              .Returns(Task.CompletedTask);

        _cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .Callback((string key, CancellationToken _) => _store.Remove(key))
              .Returns(Task.CompletedTask);
    }

    private static string HashOf(string code) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    private static IConfiguration BuildConfig(
        int maxAttempts = 5, int maxIssues = 3, int windowMinutes = 15, int replaySeconds = 90) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:TwoFactor:MaxAttemptsPerChallenge"] = maxAttempts.ToString(),
            ["Auth:TwoFactor:MaxIssuesPerWindow"]      = maxIssues.ToString(),
            ["Auth:TwoFactor:IssueWindowMinutes"]      = windowMinutes.ToString(),
            ["Auth:TwoFactor:TotpReplayWindowSeconds"] = replaySeconds.ToString()
        }).Build();

    private TwoFactorService BuildService(IConfiguration? config = null) =>
        new(_tokens.Object, _userManager.Object, _email.Object, _sms.Object,
            _encryption.Object, _cache.Object, _clock.Object, config ?? BuildConfig());

    private static ApplicationUser BuildUser(
        TwoFactorChannel preferred = TwoFactorChannel.Email,
        bool             phoneConfirmed = true,
        bool             authenticatorEnrolled = true) => new()
    {
        Id                        = UserId,
        Email                     = Email,
        PreferredTwoFactorChannel = preferred,
        TwoFactorPhoneConfirmed   = phoneConfirmed,
        TwoFactorPhoneEncrypted   = phoneConfirmed ? EncryptedPhone : null,
        TwoFactorPhoneLast4       = phoneConfirmed ? "2671" : null,
        TwoFactorEnrolledAt       = authenticatorEnrolled ? FixedNow.AddDays(-1) : null
    };

    private ChallengeClaims Claims(
        TwoFactorChannel channel = TwoFactorChannel.Email,
        string           jti     = "jti-1",
        string           code    = Code) => new(
            Jti:          jti,
            UserId:       UserId,
            Email:        Email,
            Purpose:      TwoFactorPurpose.Login,
            OperationKey: null,
            Channel:      channel,
            CodeHash:     channel == TwoFactorChannel.Authenticator ? null : HashOf(code),
            IssuedAt:     FixedNow,
            ExpiresAt:    FixedNow.AddMinutes(5));

    private void SetupValidate(string token, ChallengeClaims claims) =>
        _tokens.Setup(t => t.Validate(token, It.IsAny<TwoFactorPurpose>(), It.IsAny<string?>(), It.IsAny<bool>()))
               .Returns(Result<ChallengeClaims>.Success(claims));

    private void VerifyNothingDispatched()
    {
        _email.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
        _sms.Verify(s => s.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── 1-4. selección de canal ──────────────────────────────────────────────

    [Fact]
    public async Task IssueAsync_UsesPreferredChannel_WhenNoneForced()
    {
        var user    = BuildUser(preferred: TwoFactorChannel.Sms);
        var service = BuildService();

        var result = await service.IssueAsync(user, TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.Channel.Should().Be(TwoFactorChannel.Sms);
    }

    [Fact]
    public async Task IssueAsync_UsesForcedChannel_WhenGiven()
    {
        var user    = BuildUser(preferred: TwoFactorChannel.Email);
        var service = BuildService();

        var result = await service.IssueAsync(
            user, TwoFactorPurpose.Login, forcedChannel: TwoFactorChannel.Sms);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.Channel.Should().Be(TwoFactorChannel.Sms);

        _email.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IssueAsync_WhenSmsRequestedButPhoneNotConfirmed_ReturnsChannelUnavailable()
    {
        var user    = BuildUser(phoneConfirmed: false);
        var service = BuildService();

        var result = await service.IssueAsync(
            user, TwoFactorPurpose.Login, forcedChannel: TwoFactorChannel.Sms);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CHANNEL_UNAVAILABLE");
        VerifyNothingDispatched();
    }

    [Fact]
    public async Task IssueAsync_WhenAuthenticatorRequestedButNotEnrolled_ReturnsChannelUnavailable()
    {
        var user    = BuildUser(authenticatorEnrolled: false);
        var service = BuildService();

        var result = await service.IssueAsync(
            user, TwoFactorPurpose.Login, forcedChannel: TwoFactorChannel.Authenticator);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CHANNEL_UNAVAILABLE");
    }

    // ── 5-8. despacho ────────────────────────────────────────────────────────

    [Fact]
    public async Task IssueAsync_WhenEmail_SendsEmailWithCode()
    {
        var user    = BuildUser(preferred: TwoFactorChannel.Email);
        var service = BuildService();

        var result = await service.IssueAsync(user, TwoFactorPurpose.Login, languageCode: "es");

        result.IsSuccess.Should().BeTrue(because: result.Error);

        _email.Verify(e => e.SendAsync(
            Email,
            It.IsAny<string>(),
            "es",
            NotificationEvents.TwoFactorCode,
            It.Is<Dictionary<string, string>>(v => v["Code"] == Code),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IssueAsync_WhenSms_SendsSms()
    {
        var user    = BuildUser(preferred: TwoFactorChannel.Sms);
        var service = BuildService();

        var result = await service.IssueAsync(user, TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeTrue(because: result.Error);

        _sms.Verify(s => s.SendAsync(
            Phone,
            "en",
            NotificationEvents.TwoFactorCode,
            It.Is<Dictionary<string, string>>(v => v["Code"] == Code),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IssueAsync_WhenAuthenticator_SendsNothing()
    {
        var user    = BuildUser(preferred: TwoFactorChannel.Authenticator);
        var service = BuildService();

        var result = await service.IssueAsync(user, TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.Channel.Should().Be(TwoFactorChannel.Authenticator);
        VerifyNothingDispatched();

        // El código lo genera la aplicación del usuario: el challenge no lleva hash.
        _tokens.Verify(t => t.Issue(
            UserId, Email, TwoFactorPurpose.Login, TwoFactorChannel.Authenticator, null, null), Times.Once);
    }

    [Fact]
    public async Task IssueAsync_WhenTransportThrows_ReturnsChannelUnavailable_AndDoesNotIssue()
    {
        var user    = BuildUser(preferred: TwoFactorChannel.Email);
        var service = BuildService();

        _email.Setup(e => e.SendAsync(
                  It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                  It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("SMTP down"));

        var result = await service.IssueAsync(user, TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CHANNEL_UNAVAILABLE");

        // Nada de devolver un challenge por un código que nunca salió.
        _tokens.Verify(t => t.Issue(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TwoFactorPurpose>(),
            It.IsAny<TwoFactorChannel>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    // ── 9-10. enmascarado ────────────────────────────────────────────────────

    [Fact]
    public async Task IssueAsync_MasksEmail()
    {
        var user    = BuildUser(preferred: TwoFactorChannel.Email);
        var service = BuildService();

        var result = await service.IssueAsync(user, TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.MaskedTarget.Should().Be("u******@dominio.com");
    }

    [Fact]
    public async Task IssueAsync_MasksPhone()
    {
        var user    = BuildUser(preferred: TwoFactorChannel.Sms);
        var service = BuildService();

        var result = await service.IssueAsync(user, TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.MaskedTarget.Should().EndWith("2671");
        result.Value!.MaskedTarget.Should().NotContain("4155");
        result.Value!.MaskedTarget.Should().HaveLength(Phone.Length);
    }

    // ── 11-15. verificación ──────────────────────────────────────────────────

    [Fact]
    public async Task VerifyAsync_WhenEmailCodeCorrect_Succeeds()
    {
        SetupValidate(Token, Claims());
        var service = BuildService();

        var result = await service.VerifyAsync(Token, Code, TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.UserId.Should().Be(UserId);
        result.Value!.Jti.Should().Be("jti-1");
    }

    [Fact]
    public async Task VerifyAsync_WhenEmailCodeWrong_Fails()
    {
        SetupValidate(Token, Claims());
        var service = BuildService();

        var result = await service.VerifyAsync(Token, "654321", TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CODE_INVALID");
    }

    [Fact]
    public async Task VerifyAsync_WhenAuthenticatorCode_DelegatesToIdentity()
    {
        var user = BuildUser(preferred: TwoFactorChannel.Authenticator);
        SetupValidate(Token, Claims(TwoFactorChannel.Authenticator));

        _userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);
        _userManager.Setup(m => m.VerifyTwoFactorTokenAsync(
                        user, TokenOptions.DefaultAuthenticatorProvider, Code))
                    .ReturnsAsync(true);

        var service = BuildService();

        var result = await service.VerifyAsync(Token, Code, TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeTrue(because: result.Error);

        // TOTP lo verifica Identity contra la clave del usuario, no el hash del challenge.
        _userManager.Verify(m => m.VerifyTwoFactorTokenAsync(
            user, TokenOptions.DefaultAuthenticatorProvider, Code), Times.Once);
    }

    [Fact]
    public async Task VerifyAsync_WhenChallengeAlreadyConsumed_Fails()
    {
        SetupValidate(Token, Claims());
        var service = BuildService();

        var first = await service.VerifyAsync(Token, Code, TwoFactorPurpose.Login);
        first.IsSuccess.Should().BeTrue(because: first.Error);

        var second = await service.VerifyAsync(Token, Code, TwoFactorPurpose.Login);

        second.IsSuccess.Should().BeFalse();
        second.ErrorCode.Should().Be("INVALID_CHALLENGE");
    }

    [Fact]
    public async Task VerifyAsync_WhenTotpCodeReused_Fails()
    {
        var user = BuildUser(preferred: TwoFactorChannel.Authenticator);

        // Dos challenges distintos: el antirreplay del challenge no cubre esto, porque el jti
        // cambia. Lo que no puede repetirse es el código que Identity sigue aceptando.
        SetupValidate("token-1", Claims(TwoFactorChannel.Authenticator, jti: "jti-1"));
        SetupValidate("token-2", Claims(TwoFactorChannel.Authenticator, jti: "jti-2"));

        _userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);
        _userManager.Setup(m => m.VerifyTwoFactorTokenAsync(
                        user, TokenOptions.DefaultAuthenticatorProvider, Code))
                    .ReturnsAsync(true);

        var service = BuildService();

        var first = await service.VerifyAsync("token-1", Code, TwoFactorPurpose.Login);
        first.IsSuccess.Should().BeTrue(because: first.Error);

        var second = await service.VerifyAsync("token-2", Code, TwoFactorPurpose.Login);

        second.IsSuccess.Should().BeFalse();
        second.ErrorCode.Should().Be("CODE_INVALID");
    }

    // ── 16-17. límites ───────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyAsync_AfterFiveFailedAttempts_BurnsChallenge()
    {
        SetupValidate(Token, Claims());
        var service = BuildService();

        for (var i = 0; i < 5; i++)
        {
            var failed = await service.VerifyAsync(Token, "000000", TwoFactorPurpose.Login);
            failed.IsSuccess.Should().BeFalse();
        }

        // El sexto intento falla aunque el código sea el bueno: hay que pedir otro challenge.
        var result = await service.VerifyAsync(Token, Code, TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOO_MANY_ATTEMPTS");
    }

    [Fact]
    public async Task IssueAsync_AfterThreeIssuesInWindow_ReturnsTooManyRequests()
    {
        var user    = BuildUser(preferred: TwoFactorChannel.Sms);
        var service = BuildService();

        for (var i = 0; i < 3; i++)
        {
            var issued = await service.IssueAsync(user, TwoFactorPurpose.Login);
            issued.IsSuccess.Should().BeTrue(because: issued.Error);
        }

        var result = await service.IssueAsync(user, TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOO_MANY_REQUESTS");

        // El cuarto SMS no se manda: cada mensaje se paga.
        _sms.Verify(s => s.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}
