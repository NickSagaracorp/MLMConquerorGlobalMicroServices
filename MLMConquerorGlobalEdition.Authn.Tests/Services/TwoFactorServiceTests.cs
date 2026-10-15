using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Authn.Services;
using MLMConquerorGlobalEdition.Authn.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Security;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;

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

    /// <summary>Contadores de <c>IncrementAsync</c>, aparte del almacén de valores: en la
    /// caché real viven en otro espacio (INCR sobre un string, no una entrada JSON).</summary>
    private readonly Dictionary<string, long> _counters = new();

    /// <summary>Expiración con la que se creó cada contador, para poder afirmar que no se
    /// reescribe en los incrementos siguientes.</summary>
    private readonly Dictionary<string, TimeSpan> _counterExpiries = new();

    /// <summary>Marcas de un solo uso de <c>TryClaimOnceAsync</c>: SET NX, otro espacio más.</summary>
    private readonly HashSet<string> _claims = new();

    /// <summary>
    /// Las pruebas de carrera lanzan verificaciones a la vez: el falso Redis tiene que ser atómico
    /// él mismo, o lo que se probaría sería el diccionario.
    /// </summary>
    private readonly object _gate = new();

    /// <summary>
    /// Si se fija, cada reclamo espera a que se cumpla antes de decidir: así las dos verificaciones
    /// de la prueba de carrera llegan las dos al reclamo con el código ya validado.
    /// </summary>
    private Func<Task>? _beforeClaim;

    public TwoFactorServiceTests()
    {
        MoveClockTo(FixedNow);

        _tokens.SetupGet(t => t.ChallengeLifetime).Returns(TimeSpan.FromMinutes(5));
        _tokens.Setup(t => t.GenerateCode()).Returns(Code);
        // El doble compara contra un SHA-256 a secas solo por comodidad; la huella de verdad
        // (HMAC con clave, atada al jti) se prueba en ChallengeTokenServiceTests y de punta a
        // punta más abajo con el emisor real.
        _tokens.Setup(t => t.VerifyCode(It.IsAny<ChallengeClaims>(), It.IsAny<string>()))
               .Returns((ChallengeClaims c, string code) => c.CodeHash is not null && c.CodeHash == HashOf(code));
        _tokens.Setup(t => t.TotpReplayTag(It.IsAny<string>(), It.IsAny<string>()))
               .Returns<string, string>(TagOf);
        _tokens.Setup(t => t.Issue(
                   It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TwoFactorPurpose>(),
                   It.IsAny<TwoFactorChannel>(), It.IsAny<string?>(), It.IsAny<string?>()))
               .Returns(Token);

        _encryption.Setup(e => e.Decrypt(It.IsAny<string>())).Returns(Phone);

        WireCache<string>();
        WireCounters();
        WireClaims();
    }

    /// <summary>Simula SET NX: el primero que reclama la clave gana, cualquier otro pierde.</summary>
    private void WireClaims() =>
        _cache.Setup(c => c.TryClaimOnceAsync(
                  It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
              .Returns(async (string key, TimeSpan _, CancellationToken _) =>
              {
                  if (_beforeClaim is not null)
                      await _beforeClaim();

                  lock (_gate)
                      return _claims.Add(key);
              });

    /// <summary>
    /// Simula INCR + EXPIRE: devuelve el valor nuevo y solo se queda con la expiración de la
    /// primera llamada, que es lo que hace Redis cuando el TTL se fija al crear el contador.
    /// </summary>
    private void WireCounters()
    {
        _cache.Setup(c => c.IncrementAsync(
                  It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((string key, TimeSpan expiry, CancellationToken _) =>
              {
                  lock (_gate)
                  {
                      if (!_counters.ContainsKey(key))
                          _counterExpiries[key] = expiry;

                      var next = _counters.TryGetValue(key, out var current) ? current + 1 : 1;
                      _counters[key] = next;
                      return next;
                  }
              });

        _cache.Setup(c => c.DecrementAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((string key, CancellationToken _) =>
              {
                  lock (_gate)
                  {
                      if (!_counters.TryGetValue(key, out var current))
                          return 0;

                      var next = current - 1;
                      if (next <= 0)
                      {
                          _counters.Remove(key);
                          _counterExpiries.Remove(key);
                      }
                      else
                      {
                          _counters[key] = next;
                      }
                      return next;
                  }
              });

        _cache.Setup(c => c.GetCounterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((string key, CancellationToken _) =>
              {
                  lock (_gate)
                      return _counters.TryGetValue(key, out var current) ? current : 0;
              });
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

    private static string TagOf(string userId, string code) => $"tag({userId},{code})";

    private static IConfiguration BuildConfig(
        int maxAttempts = 5, int maxIssues = 3, int windowMinutes = 15, int replaySeconds = 180) =>
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

    /// <summary>
    /// Teléfono guardado pero todavía sin confirmar: es el estado en el que queda una cuenta
    /// justo después de dar de alta el número, y es el único momento en el que hace falta
    /// mandarle un SMS para poder confirmarlo. Sin esta excepción por propósito, dar de alta un
    /// teléfono sería imposible sin saltarse la librería —y con ella, el tope de emisiones.
    /// </summary>
    [Fact]
    public async Task IssueAsync_WhenSmsUnconfirmed_AndPurposeIsEnrollment_SendsTheCode()
    {
        var user    = BuildUserWithUnconfirmedPhone();
        var service = BuildService();

        var result = await service.IssueAsync(
            user, TwoFactorPurpose.Enrollment, forcedChannel: TwoFactorChannel.Sms);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.Channel.Should().Be(TwoFactorChannel.Sms);

        _sms.Verify(s => s.SendAsync(
            Phone, It.IsAny<string>(), NotificationEvents.TwoFactorCode,
            It.Is<Dictionary<string, string>>(v => v["Code"] == Code),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// La excepción anterior vale solo para el enrolamiento. Un teléfono sin confirmar no es un
    /// segundo factor: nadie ha demostrado tenerlo, así que no puede abrir una sesión. Si el
    /// login lo aceptara, bastaría con dar de alta cualquier número para desviar ahí los códigos.
    /// </summary>
    [Fact]
    public async Task IssueAsync_WhenSmsUnconfirmed_AndPurposeIsLogin_ReturnsChannelUnavailable()
    {
        var user    = BuildUserWithUnconfirmedPhone();
        var service = BuildService();

        var result = await service.IssueAsync(
            user, TwoFactorPurpose.Login, forcedChannel: TwoFactorChannel.Sms);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CHANNEL_UNAVAILABLE");
        VerifyNothingDispatched();
    }

    /// <summary>
    /// Ni siquiera el enrolamiento resuelve un canal sin destino: sin número guardado no hay a
    /// dónde mandar nada.
    /// </summary>
    [Fact]
    public async Task IssueAsync_WhenNoPhoneStored_AndPurposeIsEnrollment_ReturnsChannelUnavailable()
    {
        var user    = BuildUser(phoneConfirmed: false);
        var service = BuildService();

        var result = await service.IssueAsync(
            user, TwoFactorPurpose.Enrollment, forcedChannel: TwoFactorChannel.Sms);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CHANNEL_UNAVAILABLE");
        VerifyNothingDispatched();
    }

    /// <summary>Teléfono guardado, pendiente de confirmar: el estado intermedio del alta.</summary>
    private static ApplicationUser BuildUserWithUnconfirmedPhone() => new()
    {
        Id                        = UserId,
        Email                     = Email,
        PreferredTwoFactorChannel = TwoFactorChannel.Email,
        TwoFactorPhoneEncrypted   = EncryptedPhone,
        TwoFactorPhoneLast4       = "2671",
        TwoFactorPhoneConfirmed   = false
    };

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

    [Fact]
    public async Task IssueAsync_WhenTransportThrows_RefundsIssueQuota()
    {
        var user    = BuildUser(preferred: TwoFactorChannel.Email);
        var service = BuildService();

        _email.Setup(e => e.SendAsync(
                  It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                  It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("SMTP down"));

        await service.IssueAsync(user, TwoFactorPurpose.Login);

        // El cupo se apunta antes de despachar, porque si no el tope no sería real. Como
        // aquí no salió ningún mensaje, hay que devolverlo: sin esto una caída de SES
        // consumiría las tres emisiones del usuario y lo dejaría fuera quince minutos,
        // sin poder probar siquiera otro canal.
        _cache.Verify(c => c.DecrementAsync(
            CacheKeys.TwoFactorIssueWindow(user.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IssueAsync_WhenDispatchSucceeds_DoesNotRefundQuota()
    {
        var user    = BuildUser(preferred: TwoFactorChannel.Email);
        var service = BuildService();

        var result = await service.IssueAsync(user, TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        _cache.Verify(c => c.DecrementAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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

    // ── 18-22. los contadores son incrementos atómicos ───────────────────────
    //
    // Lo que se puede afirmar sin un Redis de verdad es que el servicio pide un incremento en
    // vez de un leer-modificar-escribir, y que la expiración se manda solo al crear. La
    // atomicidad la aporta el backend; probarla con hilos contra un diccionario no demostraría
    // nada y sería intermitente.

    [Fact]
    public async Task IssueAsync_CountsIssues_WithAtomicIncrement_NotReadModifyWrite()
    {
        var user    = BuildUser(preferred: TwoFactorChannel.Email);
        var service = BuildService();
        var key     = CacheKeys.TwoFactorIssueWindow(UserId);

        var result = await service.IssueAsync(user, TwoFactorPurpose.Login);

        result.IsSuccess.Should().BeTrue(because: result.Error);

        _cache.Verify(c => c.IncrementAsync(key, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
                      Times.Once);

        // Ni lectura previa ni escritura posterior: entre las dos cabía la ráfaga concurrente.
        _cache.Verify(c => c.GetAsync<string>(key, It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.SetAsync(key, It.IsAny<string>(), It.IsAny<TimeSpan>(),
                                      It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VerifyAsync_CountsAttempts_WithAtomicIncrement_NotReadModifyWrite()
    {
        SetupValidate(Token, Claims());
        var service = BuildService();
        var key     = CacheKeys.TwoFactorAttempts("jti-1");

        var result = await service.VerifyAsync(Token, "654321", TwoFactorPurpose.Login);

        result.ErrorCode.Should().Be("CODE_INVALID");

        _cache.Verify(c => c.IncrementAsync(key, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
                      Times.Once);
        _cache.Verify(c => c.GetAsync<string>(key, It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.SetAsync(key, It.IsAny<string>(), It.IsAny<TimeSpan>(),
                                      It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IssueAsync_SendsIssueWindowAsExpiry_SoTheWindowDoesNotStretch()
    {
        var user    = BuildUser(preferred: TwoFactorChannel.Email);
        var service = BuildService(BuildConfig(maxIssues: 3, windowMinutes: 15));
        var key     = CacheKeys.TwoFactorIssueWindow(UserId);

        // Tres emisiones separadas en el tiempo: si el TTL se renovara en cada una, la ventana
        // se estiraría sola y "3 cada 15 minutos" pasaría a ser "3 y luego silencio".
        for (var i = 0; i < 3; i++)
        {
            MoveClockTo(FixedNow.AddMinutes(i * 5));
            var issued = await service.IssueAsync(user, TwoFactorPurpose.Login);
            issued.IsSuccess.Should().BeTrue(because: issued.Error);
        }

        // La expiración que llega al contador es siempre la ventana completa; el backend la
        // aplica solo al crearlo, y el falso INCR de estas pruebas conserva la primera.
        _counterExpiries[key].Should().Be(TimeSpan.FromMinutes(15));
        _counters[key].Should().Be(3);
    }

    [Fact]
    public async Task VerifyAsync_AttemptCounterExpiry_IsTheChallengeRemainingLife()
    {
        SetupValidate(Token, Claims());
        var service = BuildService();
        var key     = CacheKeys.TwoFactorAttempts("jti-1");

        await service.VerifyAsync(Token, "000000", TwoFactorPurpose.Login);

        // El challenge nace en FixedNow y vive 5 minutos; el reloj no se ha movido.
        _counterExpiries[key].Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task VerifyAsync_SixthAttempt_IsRejectedByTheIncrementedCounter()
    {
        SetupValidate(Token, Claims());
        var service = BuildService();
        var key     = CacheKeys.TwoFactorAttempts("jti-1");

        for (var i = 0; i < 5; i++)
            await service.VerifyAsync(Token, "000000", TwoFactorPurpose.Login);

        var result = await service.VerifyAsync(Token, Code, TwoFactorPurpose.Login);

        result.ErrorCode.Should().Be("TOO_MANY_ATTEMPTS");

        // Seis intentos, seis incrementos: el tope se decide sobre el número que devuelve el
        // contador, no sobre un valor leído antes de escribirlo.
        _counters[key].Should().Be(6);
    }

    // ── token de enrolamiento ────────────────────────────────────────────────

    /// <summary>
    /// Con un <see cref="ChallengeTokenService"/> de verdad, no con el doble: lo que hay que
    /// comprobar es que el propósito grabado en el token separa de verdad el enrolamiento del
    /// login, y eso vive en el emisor. Un mock devolvería la cadena que le pidiéramos y la
    /// prueba pasaría aunque el propósito fuera el equivocado.
    /// </summary>
    private static (ChallengeTokenService Service, IConfiguration Config) RealChallengeService(
        IDateTimeProvider clock)
    {
        using var rsa = RSA.Create(2048);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:PrivateKeyBase64"] = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()),
            ["Jwt:PublicKeyBase64"]  = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()),
            ["Jwt:Issuer"]           = "MLMConqueror",
            ["Jwt:Audience"]         = "MLMConquerorUsers"
        }).Build();

        return (new ChallengeTokenService(config, clock), config);
    }

    [Fact]
    public void IssueEnrollmentToken_ValidatesAsEnrollment_AndNotAsLogin()
    {
        var (challenges, config) = RealChallengeService(_clock.Object);
        var service = new TwoFactorService(
            challenges, _userManager.Object, _email.Object, _sms.Object,
            _encryption.Object, _cache.Object, _clock.Object, config);

        var token = service.IssueEnrollmentToken(BuildUser());

        token.Should().NotBeNullOrWhiteSpace();

        var asEnrollment = challenges.Validate(token, TwoFactorPurpose.Enrollment);
        asEnrollment.IsSuccess.Should().BeTrue();
        asEnrollment.Value!.UserId.Should().Be(UserId);
        asEnrollment.Value.Channel.Should().Be(TwoFactorChannel.Authenticator);

        // Sin código enviado no hay hash contra el que comparar: el token autoriza a
        // enrolarse, no a redimir un código.
        asEnrollment.Value.CodeHash.Should().BeNull();

        // Lo que de verdad protege: presentado a un endpoint de login, muere. Si valiera para
        // login, el "token de enrolamiento" sería un pase de acceso encubierto para quien
        // todavía no ha configurado su segundo factor.
        challenges.Validate(token, TwoFactorPurpose.Login).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void IssueEnrollmentToken_DispatchesNothing_AndSpendsNoIssueQuota()
    {
        var (challenges, config) = RealChallengeService(_clock.Object);
        var service = new TwoFactorService(
            challenges, _userManager.Object, _email.Object, _sms.Object,
            _encryption.Object, _cache.Object, _clock.Object, config);

        service.IssueEnrollmentToken(BuildUser());

        VerifyNothingDispatched();
        _counters.Should().BeEmpty();
    }

    // ── canje atómico (Plan E, Tarea 4b) ─────────────────────────────────────

    /// <summary>
    /// LA CARRERA. Dos canjes del mismo reto con el código bueno, a la vez. Con el par "¿está
    /// usado? → marcar" los dos leían "no usado" y los dos salían con éxito: dos permisos de step-up
    /// por un código. Aquí los dos llegan al reclamo con el código ya validado —el reclamo espera a
    /// que estén los dos— y solo uno puede ganarlo.
    /// </summary>
    [Fact]
    public async Task VerifyAsync_DosCanjesSimultaneosDelMismoReto_SoloUnoGana()
    {
        SetupValidate(Token, Claims());
        var service = BuildService();

        var bothArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived     = 0;
        _beforeClaim = () =>
        {
            if (Interlocked.Increment(ref arrived) == 2)
                bothArrived.SetResult();
            return bothArrived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        };

        var results = await Task.WhenAll(
            Task.Run(() => service.VerifyAsync(Token, Code, TwoFactorPurpose.Login)),
            Task.Run(() => service.VerifyAsync(Token, Code, TwoFactorPurpose.Login)));

        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Single(r => !r.IsSuccess).ErrorCode.Should().Be("INVALID_CHALLENGE");
    }

    [Fact]
    public async Task VerifyAsync_ReclamaElRetoDespuesDeVerificar_NoAntes()
    {
        SetupValidate(Token, Claims());
        var service = BuildService();

        // Un código malo no puede quemar el reto: el usuario todavía puede teclear el bueno.
        (await service.VerifyAsync(Token, "654321", TwoFactorPurpose.Login)).ErrorCode.Should().Be("CODE_INVALID");
        _claims.Should().BeEmpty();

        (await service.VerifyAsync(Token, Code, TwoFactorPurpose.Login)).IsSuccess.Should().BeTrue();
        _claims.Should().Contain(CacheKeys.TwoFactorChallengeConsumed("jti-1"));
    }

    [Fact]
    public async Task VerifyAsync_UsaElReclamoAtomico_NoLeerYEscribir()
    {
        SetupValidate(Token, Claims());
        var service = BuildService();
        var key     = CacheKeys.TwoFactorChallengeConsumed("jti-1");

        await service.VerifyAsync(Token, Code, TwoFactorPurpose.Login);

        _cache.Verify(c => c.TryClaimOnceAsync(key, TimeSpan.FromMinutes(5), It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.GetAsync<string>(key, It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.SetAsync(key, It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VerifyAsync_SiElAlmacenNoResponde_FallaCerrado_SinLanzar()
    {
        SetupValidate(Token, Claims());
        _cache.Setup(c => c.TryClaimOnceAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("Redis caído"));
        var service = BuildService();

        var result = await service.VerifyAsync(Token, Code, TwoFactorPurpose.Login);

        // Sin poder marcar el reto, el mismo código valdría otra vez: no hay éxito posible.
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VERIFICATION_UNAVAILABLE");
    }

    [Fact]
    public async Task VerifyAsync_TotpReclamaElCodigoTambien()
    {
        var user = BuildUser(preferred: TwoFactorChannel.Authenticator);
        SetupValidate(Token, Claims(TwoFactorChannel.Authenticator));
        _userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);
        _userManager.Setup(m => m.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, Code))
                    .ReturnsAsync(true);
        var service = BuildService(BuildConfig(replaySeconds: 150));

        (await service.VerifyAsync(Token, Code, TwoFactorPurpose.Login)).IsSuccess.Should().BeTrue();

        _cache.Verify(c => c.TryClaimOnceAsync(
            CacheKeys.TwoFactorTotpUsed(UserId, TagOf(UserId, Code)), TimeSpan.FromSeconds(150), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyAsync_AlAgotarLosIntentos_QuemaConElMismoReclamo()
    {
        SetupValidate(Token, Claims());
        var service = BuildService();

        for (var i = 0; i < 5; i++)
            await service.VerifyAsync(Token, "000000", TwoFactorPurpose.Login);

        // La quema tiene que vivir donde mira el reclamo: con Redis, SetAsync y SET NX no comparten
        // espacio de claves.
        _claims.Should().Contain(CacheKeys.TwoFactorChallengeConsumed("jti-1"));
    }

    // ── plantilla y cupo del step-up ─────────────────────────────────────────

    [Theory]
    [InlineData(TwoFactorChannel.Email)]
    [InlineData(TwoFactorChannel.Sms)]
    public async Task IssueAsync_StepUp_UsaSuPropiaPlantilla(TwoFactorChannel channel)
    {
        var user    = BuildUser(preferred: channel);
        var service = BuildService();

        var result = await service.IssueAsync(user, TwoFactorPurpose.StepUp, StepUpOperations.PayoutMethodChange);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        if (channel == TwoFactorChannel.Email)
            _email.Verify(e => e.SendAsync(Email, It.IsAny<string>(), It.IsAny<string>(), NotificationEvents.StepUpCode,
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
        else
            _sms.Verify(s => s.SendAsync(Phone, It.IsAny<string>(), NotificationEvents.StepUpCode,
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IssueAsync_Login_SigueConLaPlantillaDeLogin()
    {
        var service = BuildService();

        await service.IssueAsync(BuildUser(), TwoFactorPurpose.Login);

        _email.Verify(e => e.SendAsync(Email, It.IsAny<string>(), It.IsAny<string>(), NotificationEvents.StepUpCode,
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IssueAsync_StepUp_CuentaEnSuPropioCupo_YNoAgotaElDelLogin()
    {
        var user    = BuildUser();
        var service = BuildService(BuildConfig(maxIssues: 3));

        for (var i = 0; i < 3; i++)
            (await service.IssueAsync(user, TwoFactorPurpose.StepUp, StepUpOperations.PayoutMethodChange)).IsSuccess.Should().BeTrue();

        (await service.IssueAsync(user, TwoFactorPurpose.StepUp, StepUpOperations.PayoutMethodChange))
            .ErrorCode.Should().Be("TOO_MANY_REQUESTS");

        // El dueño sigue pudiendo entrar: pedir step-ups no gasta los códigos de su login.
        (await service.IssueAsync(user, TwoFactorPurpose.Login)).IsSuccess.Should().BeTrue();

        _counters[CacheKeys.StepUpIssueWindow(UserId)].Should().Be(4);
        _counters[CacheKeys.TwoFactorIssueWindow(UserId)].Should().Be(1);
    }

    [Fact]
    public async Task IssueAsync_StepUp_SiElTransporteFalla_DevuelveSuPropioCupo()
    {
        _email.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                  It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("SES caído"));
        var service = BuildService();

        await service.IssueAsync(BuildUser(), TwoFactorPurpose.StepUp, StepUpOperations.PayoutMethodChange);

        _cache.Verify(c => c.DecrementAsync(CacheKeys.StepUpIssueWindow(UserId), It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.DecrementAsync(CacheKeys.TwoFactorIssueWindow(UserId), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── ventana antirreplay del TOTP ─────────────────────────────────────────

    [Theory]
    [InlineData(30,  150)]
    [InlineData(90,  150)]
    [InlineData(149, 150)]
    [InlineData(150, 150)]
    [InlineData(240, 240)]
    public async Task VerifyAsync_VentanaTotp_NuncaBajaDe150Segundos(int configurado, int esperado)
    {
        // Identity acepta el código de los pasos -2..+2 de 30 s (~150 s). Una marca que caduca
        // antes deja que el mismo código vuelva a valer dentro de su propia vigencia.
        var user = BuildUser(preferred: TwoFactorChannel.Authenticator);
        SetupValidate(Token, Claims(TwoFactorChannel.Authenticator));
        _userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);
        _userManager.Setup(m => m.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, Code))
                    .ReturnsAsync(true);
        var service = BuildService(BuildConfig(replaySeconds: configurado));

        (await service.VerifyAsync(Token, Code, TwoFactorPurpose.Login)).IsSuccess.Should().BeTrue();

        _cache.Verify(c => c.TryClaimOnceAsync(
            CacheKeys.TwoFactorTotpUsed(UserId, TagOf(UserId, Code)), TimeSpan.FromSeconds(esperado),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyAsync_VentanaTotp_PorDefectoEs180Segundos()
    {
        var user = BuildUser(preferred: TwoFactorChannel.Authenticator);
        SetupValidate(Token, Claims(TwoFactorChannel.Authenticator));
        _userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);
        _userManager.Setup(m => m.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, Code))
                    .ReturnsAsync(true);
        var service = BuildService(new ConfigurationBuilder().Build());

        (await service.VerifyAsync(Token, Code, TwoFactorPurpose.Login)).IsSuccess.Should().BeTrue();

        _cache.Verify(c => c.TryClaimOnceAsync(
            CacheKeys.TwoFactorTotpUsed(UserId, TagOf(UserId, Code)), TimeSpan.FromSeconds(180),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── de punta a punta con el emisor de verdad ─────────────────────────────

    /// <summary>
    /// TwoFactorService con un <see cref="ChallengeTokenService"/> real: la huella la calcula el
    /// emisor con el jti que genera y la comprueba con el del reto presentado. Con el doble no se
    /// vería si las dos mitades dejaran de cuadrar.
    /// </summary>
    private (TwoFactorService Service, ChallengeTokenService Challenges) RealPipeline()
    {
        var (challenges, config) = RealChallengeService(_clock.Object);
        return (new TwoFactorService(
            challenges, _userManager.Object, _email.Object, _sms.Object,
            _encryption.Object, _cache.Object, _clock.Object, config), challenges);
    }

    [Theory]
    [InlineData(TwoFactorChannel.Email)]
    [InlineData(TwoFactorChannel.Sms)]
    public async Task DePuntaAPunta_CorreoYSms_ElCodigoEnviadoCanjeaYOtroNo(TwoFactorChannel channel)
    {
        var (service, challenges) = RealPipeline();
        string? enviado = null;
        _email.Setup(e => e.SendAsync(
                  It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                  It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
              .Callback((string _, string _, string _, string _, Dictionary<string, string> v, CancellationToken _) =>
                  enviado = v["Code"])
              .Returns(Task.CompletedTask);
        _sms.Setup(s => s.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string _, string _, Dictionary<string, string> v, CancellationToken _) =>
                enviado = v["Code"])
            .Returns(Task.CompletedTask);

        var issued = await service.IssueAsync(BuildUser(preferred: channel), TwoFactorPurpose.Login);
        issued.IsSuccess.Should().BeTrue(because: issued.Error);
        enviado.Should().MatchRegex("^[0-9]{6}$");

        var otro  = enviado == "000000" ? "000001" : "000000";
        var malo  = await service.VerifyAsync(issued.Value!.ChallengeToken, otro, TwoFactorPurpose.Login);
        malo.ErrorCode.Should().Be("CODE_INVALID");

        var bueno = await service.VerifyAsync(issued.Value!.ChallengeToken, enviado!, TwoFactorPurpose.Login);
        bueno.IsSuccess.Should().BeTrue(because: bueno.Error);
        bueno.Value!.Channel.Should().Be(channel);

        // Y lo que viaja en el reto no es el SHA-256 del código.
        challenges.Validate(issued.Value!.ChallengeToken, TwoFactorPurpose.Login).Value!.CodeHash
            .Should().NotBe(HashOf(enviado!));
    }

    [Fact]
    public async Task DePuntaAPunta_StepUp_ElCodigoDeOtroRetoNoSirve()
    {
        // Dos retos de step-up seguidos: el código del primero no canjea el segundo.
        var (service, _) = RealPipeline();
        var enviados = new List<string>();
        _email.Setup(e => e.SendAsync(
                  It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                  It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
              .Callback((string _, string _, string _, string _, Dictionary<string, string> v, CancellationToken _) =>
                  enviados.Add(v["Code"]))
              .Returns(Task.CompletedTask);

        var a = await service.IssueAsync(BuildUser(), TwoFactorPurpose.StepUp, "PAYOUT_WALLET_CHANGE");
        var b = await service.IssueAsync(BuildUser(), TwoFactorPurpose.StepUp, "PAYOUT_WALLET_CHANGE");
        enviados.Should().HaveCount(2);

        if (enviados[0] != enviados[1])
            (await service.VerifyAsync(b.Value!.ChallengeToken, enviados[0], TwoFactorPurpose.StepUp, "PAYOUT_WALLET_CHANGE"))
                .ErrorCode.Should().Be("CODE_INVALID");

        (await service.VerifyAsync(b.Value!.ChallengeToken, enviados[1], TwoFactorPurpose.StepUp, "PAYOUT_WALLET_CHANGE"))
            .IsSuccess.Should().BeTrue();
        (await service.VerifyAsync(a.Value!.ChallengeToken, enviados[0], TwoFactorPurpose.StepUp, "PAYOUT_WALLET_CHANGE"))
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DePuntaAPunta_Totp_CanjeaUnaVez_YLaMarcaLlevaEtiquetaConClave()
    {
        var (service, challenges) = RealPipeline();
        var user = BuildUser(preferred: TwoFactorChannel.Authenticator);
        _userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);
        _userManager.Setup(m => m.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, Code))
                    .ReturnsAsync(true);

        var a = await service.IssueAsync(user, TwoFactorPurpose.Login);
        var b = await service.IssueAsync(user, TwoFactorPurpose.Login);

        (await service.VerifyAsync(a.Value!.ChallengeToken, Code, TwoFactorPurpose.Login)).IsSuccess.Should().BeTrue();

        var repetido = await service.VerifyAsync(b.Value!.ChallengeToken, Code, TwoFactorPurpose.Login);
        repetido.ErrorCode.Should().Be("CODE_INVALID");

        var marca = CacheKeys.TwoFactorTotpUsed(UserId, challenges.TotpReplayTag(UserId, Code));
        _claims.Should().Contain(marca);
        _claims.Should().NotContain(CacheKeys.TwoFactorTotpUsed(UserId, HashOf(Code)));
    }

    // ── tope diario de códigos fallidos ──────────────────────────────────────

    private const string WrongCode = "000000";

    private static string LoginFailures  => CacheKeys.TwoFactorFailures(UserId, "login");
    private static string StepUpFailures => CacheKeys.TwoFactorFailures(UserId, "stepup");

    private ChallengeClaims StepUpClaims(string jti) =>
        Claims(jti: jti) with { Purpose = TwoFactorPurpose.StepUp, OperationKey = "PAYOUT_WALLET_CHANGE" };

    /// <summary>Cada intento con un reto nuevo, para que el límite por reto no se cruce.</summary>
    private async Task<Result<ChallengeClaims>> VerifyWithFreshChallenge(
        TwoFactorService service, int i, string code, bool stepUp = false)
    {
        var token  = $"tok-{(stepUp ? "su" : "lg")}-{i}";
        var claims = stepUp ? StepUpClaims($"jti-su-{i}") : Claims(jti: $"jti-lg-{i}");
        SetupValidate(token, claims);
        return stepUp
            ? await service.VerifyAsync(token, code, TwoFactorPurpose.StepUp, "PAYOUT_WALLET_CHANGE")
            : await service.VerifyAsync(token, code, TwoFactorPurpose.Login);
    }

    private TwoFactorService BuildServiceWith(
        ISecurityFactorNotifier? notifier = null, ILogger<TwoFactorService>? logger = null, IConfiguration? config = null) =>
        new(_tokens.Object, _userManager.Object, _email.Object, _sms.Object,
            _encryption.Object, _cache.Object, _clock.Object, config ?? BuildConfig(), logger, notifier);

    [Fact]
    public async Task Tope_diario_de_login_bloquea_la_verificacion_aunque_el_codigo_sea_bueno()
    {
        var service = BuildService();

        for (var i = 0; i < 15; i++)
            (await VerifyWithFreshChallenge(service, i, WrongCode)).ErrorCode.Should().Be("CODE_INVALID");

        _counters[LoginFailures].Should().Be(15);
        _counterExpiries[LoginFailures].Should().Be(TimeSpan.FromHours(24));

        var bloqueado = await VerifyWithFreshChallenge(service, 99, Code);
        bloqueado.IsSuccess.Should().BeFalse();
        bloqueado.ErrorCode.Should().Be("TWO_FACTOR_LOCKED");

        // Ni siquiera se miró el reto: no se gastó un intento suyo ni se reclamó su marca.
        _counters.Should().NotContainKey(CacheKeys.TwoFactorAttempts("jti-lg-99"));
        _claims.Should().NotContain(CacheKeys.TwoFactorChallengeConsumed("jti-lg-99"));
    }

    [Fact]
    public async Task Con_el_tope_alcanzado_no_se_emite_ni_se_gasta_cupo()
    {
        _counters[LoginFailures] = 15;
        var service = BuildService();

        var result = await service.IssueAsync(BuildUser(), TwoFactorPurpose.Login);

        result.ErrorCode.Should().Be("TWO_FACTOR_LOCKED");
        _counters.Should().NotContainKey(CacheKeys.TwoFactorIssueWindow(UserId));
        VerifyNothingDispatched();
    }

    [Fact]
    public async Task Los_aciertos_devuelven_la_reserva_y_no_cuentan_como_fallo()
    {
        var service = BuildService();

        (await VerifyWithFreshChallenge(service, 1, WrongCode)).ErrorCode.Should().Be("CODE_INVALID");
        (await VerifyWithFreshChallenge(service, 2, Code)).IsSuccess.Should().BeTrue();

        _counters[LoginFailures].Should().Be(1);
    }

    [Fact]
    public async Task Un_reto_repetido_no_cuenta_como_fallo()
    {
        var service = BuildService();
        (await VerifyWithFreshChallenge(service, 1, Code)).IsSuccess.Should().BeTrue();

        // El mismo reto otra vez: lo rechaza el antirreplay, no es una adivinanza.
        (await service.VerifyAsync("tok-lg-1", Code, TwoFactorPurpose.Login)).ErrorCode.Should().Be("INVALID_CHALLENGE");

        _counters.Should().NotContainKey(LoginFailures);
    }

    [Fact]
    public async Task Login_y_step_up_tienen_topes_separados()
    {
        var service = BuildService();

        for (var i = 0; i < 10; i++)
            (await VerifyWithFreshChallenge(service, i, WrongCode, stepUp: true)).ErrorCode.Should().Be("CODE_INVALID");

        (await VerifyWithFreshChallenge(service, 50, Code, stepUp: true)).ErrorCode.Should().Be("TWO_FACTOR_LOCKED");
        (await service.IssueAsync(BuildUser(), TwoFactorPurpose.StepUp, "PAYOUT_WALLET_CHANGE"))
            .ErrorCode.Should().Be("TWO_FACTOR_LOCKED");

        // Quemar el step-up no deja al dueño sin entrar.
        (await VerifyWithFreshChallenge(service, 51, Code)).IsSuccess.Should().BeTrue();
        (await service.IssueAsync(BuildUser(), TwoFactorPurpose.Login)).IsSuccess.Should().BeTrue();
        _counters.Should().NotContainKey(LoginFailures);
        _counters[StepUpFailures].Should().BeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public async Task Los_topes_se_leen_de_la_configuracion()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:TwoFactor:MaxFailuresPerDay"] = "2",
            ["Auth:StepUp:MaxFailuresPerDay"]    = "1"
        }).Build();
        var service = BuildService(config);

        await VerifyWithFreshChallenge(service, 1, WrongCode);
        await VerifyWithFreshChallenge(service, 2, WrongCode);
        (await VerifyWithFreshChallenge(service, 3, Code)).ErrorCode.Should().Be("TWO_FACTOR_LOCKED");

        await VerifyWithFreshChallenge(service, 1, WrongCode, stepUp: true);
        (await VerifyWithFreshChallenge(service, 2, Code, stepUp: true)).ErrorCode.Should().Be("TWO_FACTOR_LOCKED");
    }

    [Fact]
    public async Task Al_alcanzar_el_tope_se_avisa_al_dueno_una_sola_vez()
    {
        var user     = BuildUser();
        var notifier = new Mock<ISecurityFactorNotifier>();
        _userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);
        var service = BuildServiceWith(notifier.Object);

        for (var i = 0; i < 9; i++)
            await VerifyWithFreshChallenge(service, i, WrongCode, stepUp: true);
        notifier.Verify(n => n.NotifyTwoFactorLockedAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

        await VerifyWithFreshChallenge(service, 9, WrongCode, stepUp: true);
        for (var i = 10; i < 14; i++)
            await VerifyWithFreshChallenge(service, i, WrongCode, stepUp: true);

        notifier.Verify(n => n.NotifyTwoFactorLockedAsync(user, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Si_el_aviso_falla_el_bloqueo_se_mantiene()
    {
        var notifier = new Mock<ISecurityFactorNotifier>();
        notifier.Setup(n => n.NotifyTwoFactorLockedAsync(
                    It.IsAny<ApplicationUser>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("SES caído"));
        _userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(BuildUser());
        var service = BuildServiceWith(notifier.Object);

        for (var i = 0; i < 15; i++)
            (await VerifyWithFreshChallenge(service, i, WrongCode)).ErrorCode.Should().Be("CODE_INVALID");

        (await VerifyWithFreshChallenge(service, 99, Code)).ErrorCode.Should().Be("TWO_FACTOR_LOCKED");
    }

    [Fact]
    public async Task Si_el_contador_de_fallos_lanza_la_verificacion_se_cierra()
    {
        _cache.Setup(c => c.IncrementAsync(LoginFailures, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("Redis caído"));
        var service = BuildService();

        var result = await VerifyWithFreshChallenge(service, 1, Code);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VERIFICATION_UNAVAILABLE");
        _claims.Should().BeEmpty();
    }

    [Fact]
    public async Task Si_el_contador_de_fallos_no_se_puede_leer_no_se_emite()
    {
        _cache.Setup(c => c.GetCounterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("Redis caído"));
        var service = BuildService();

        var result = await service.IssueAsync(BuildUser(), TwoFactorPurpose.Login);

        result.ErrorCode.Should().Be("VERIFICATION_UNAVAILABLE");
        _counters.Should().NotContainKey(CacheKeys.TwoFactorIssueWindow(UserId));
        VerifyNothingDispatched();
    }

    // ── formato del código ───────────────────────────────────────────────────

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12345a")]
    [InlineData(" 23456")]
    [InlineData("123456\n")]
    [InlineData("١٢٣٤٥٦")] // dígitos arábigo-índicos: "\d" los acepta
    [InlineData("１２３４５６")] // dígitos de ancho completo
    [InlineData("")]
    public async Task Un_codigo_mal_formado_es_CODE_INVALID_sin_tocar_el_HMAC_pero_cuenta(string code)
    {
        SetupValidate(Token, Claims());
        var service = BuildService();

        var result = await service.VerifyAsync(Token, code, TwoFactorPurpose.Login);

        result.ErrorCode.Should().Be("CODE_INVALID");
        _tokens.Verify(t => t.VerifyCode(It.IsAny<ChallengeClaims>(), It.IsAny<string>()), Times.Never);
        _counters[CacheKeys.TwoFactorAttempts("jti-1")].Should().Be(1);
        _counters[LoginFailures].Should().Be(1);
    }

    [Fact]
    public async Task Un_codigo_TOTP_mal_formado_no_llega_a_Identity()
    {
        SetupValidate(Token, Claims(TwoFactorChannel.Authenticator));
        var service = BuildService();

        (await service.VerifyAsync(Token, "12 456", TwoFactorPurpose.Login)).ErrorCode.Should().Be("CODE_INVALID");

        _userManager.Verify(m => m.VerifyTwoFactorTokenAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ── avisos en el log ─────────────────────────────────────────────────────

    [Fact]
    public void Subir_la_ventana_antirreplay_del_TOTP_avisa_una_vez()
    {
        // Valor que no usa ninguna otra prueba: el aviso es una vez por valor y proceso.
        var logger = new ListLogger<TwoFactorService>();

        BuildServiceWith(logger: logger, config: BuildConfig(replaySeconds: 37));
        BuildServiceWith(logger: logger, config: BuildConfig(replaySeconds: 37));

        logger.Entries.Where(e => e.Level == LogLevel.Warning && e.Message.Contains("37"))
              .Should().ContainSingle();
    }

    [Fact]
    public void Una_ventana_valida_no_avisa()
    {
        var logger = new ListLogger<TwoFactorService>();

        BuildServiceWith(logger: logger, config: BuildConfig(replaySeconds: 151));

        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Quemar_un_reto_deja_un_aviso()
    {
        SetupValidate(Token, Claims());
        var logger  = new ListLogger<TwoFactorService>();
        var service = BuildServiceWith(logger: logger);

        for (var i = 0; i < 5; i++)
            await service.VerifyAsync(Token, WrongCode, TwoFactorPurpose.Login);

        logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message.Contains("quemado"));
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (Entries)
                Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
