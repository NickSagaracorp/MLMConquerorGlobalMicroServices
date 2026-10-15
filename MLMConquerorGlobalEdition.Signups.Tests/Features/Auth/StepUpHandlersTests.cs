using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.StepUp;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

/// <summary>
/// Reto y canje del step-up. Lo que importa: no se emite para operaciones inventadas, el permiso
/// solo sale para el mismo usuario y la misma operación del reto, y durante la espera tras un
/// cambio de factor no hay ni reto ni permiso.
/// </summary>
public class StepUpHandlersTests : IDisposable
{
    private const string UserId = "user-001";
    private const string Email  = "usuario@dominio.com";
    private const string Op     = StepUpOperations.PayoutMethodChange;

    // Hora de servidor, sin Kind UTC: la marca y la espera se miden con Now.
    private static readonly DateTime FixedNow = new(2026, 10, 14, 10, 0, 0, DateTimeKind.Local);

    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Mock<ITwoFactorService>   _twoFactor = new();
    private readonly Mock<IStepUpGrantService> _grants    = new();
    private readonly Mock<IDateTimeProvider>   _clock     = new();
    private readonly ApplicationUser _user = new() { Id = UserId, Email = Email, IsActive = true };
    private IConfiguration _config = new ConfigurationBuilder().Build();

    public StepUpHandlersTests() => _clock.Setup(c => c.Now).Returns(FixedNow);

    public void Dispose() => _db.Dispose();

    private Mock<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>> Users(ApplicationUser? user = null)
    {
        var um = UserManagerHelper.Create();
        um.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user ?? _user);
        return um;
    }

    private Mock<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>> NoUsers()
    {
        var um = UserManagerHelper.Create();
        um.Setup(m => m.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        return um;
    }

    private IssueStepUpHandler Issuer(Mock<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>? users = null) =>
        new((users ?? Users()).Object, _twoFactor.Object, _db, _config, _clock.Object,
            NullLogger<IssueStepUpHandler>.Instance);

    private VerifyStepUpHandler Verifier(Mock<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>? users = null) =>
        new(_twoFactor.Object, _grants.Object, (users ?? Users()).Object, _config, _clock.Object,
            NullLogger<VerifyStepUpHandler>.Instance);

    private static IssueStepUpCommand IssueCmd(string op = Op) =>
        new(UserId, new StepUpChallengeRequest { OperationKey = op });

    private static VerifyStepUpCommand VerifyCmd(string op = Op, string code = "123456") =>
        new(UserId, new StepUpVerifyRequest { OperationKey = op, ChallengeToken = "reto", Code = code });

    private static ChallengeClaims Claims(string userId = UserId, string? op = Op) => new(
        Jti: "jti", UserId: userId, Email: Email, Purpose: TwoFactorPurpose.StepUp,
        OperationKey: op, Channel: TwoFactorChannel.Email, CodeHash: "h",
        IssuedAt: FixedNow, ExpiresAt: FixedNow.AddMinutes(5));

    private void IssueSucceeds() =>
        _twoFactor.Setup(t => t.IssueAsync(_user, TwoFactorPurpose.StepUp, Op, null, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<ChallengeIssued>.Success(
                      new ChallengeIssued("reto", TwoFactorChannel.Email, "u***@dominio.com", FixedNow.AddMinutes(5))));

    private void VerifySucceeds() =>
        _twoFactor.Setup(t => t.VerifyAsync("reto", "123456", TwoFactorPurpose.StepUp, Op, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<ChallengeClaims>.Success(Claims()));

    // ── emisión ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reto_para_operacion_desconocida_no_emite_nada()
    {
        var result = await Issuer().Handle(IssueCmd("inventada"), default);

        result.ErrorCode.Should().Be("UNKNOWN_OPERATION");
        _twoFactor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reto_se_emite_con_proposito_step_up_y_la_operacion()
    {
        IssueSucceeds();

        var result = await Issuer().Handle(IssueCmd(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ChallengeToken.Should().Be("reto");
        result.Value.Channel.Should().Be("Email");
        result.Value.MaskedTarget.Should().Be("u***@dominio.com");
    }

    [Fact]
    public async Task Reto_para_usuario_inexistente_no_emite_nada()
    {
        var result = await Issuer(NoUsers()).Handle(IssueCmd(), default);

        result.ErrorCode.Should().Be("USER_NOT_FOUND");
        _twoFactor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reto_para_usuario_inactivo_no_emite_nada()
    {
        _user.IsActive = false;

        var result = await Issuer().Handle(IssueCmd(), default);

        result.ErrorCode.Should().Be("USER_NOT_FOUND");
        _twoFactor.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("TOO_MANY_REQUESTS")]
    [InlineData("CHANNEL_UNAVAILABLE")]
    public async Task Reto_propaga_el_fallo_de_la_emision(string code)
    {
        _twoFactor.Setup(t => t.IssueAsync(_user, TwoFactorPurpose.StepUp, Op, null, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<ChallengeIssued>.Failure(code, "x"));

        var result = await Issuer().Handle(IssueCmd(), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(code);
    }

    [Fact]
    public async Task Reto_llega_con_el_idioma_del_miembro()
    {
        _user.MemberProfileId = "AMB-000001";
        _db.MemberProfiles.Add(new MemberProfile { MemberId = "AMB-000001", DefaultLanguage = "es", CreatedBy = "t" });
        await _db.SaveChangesAsync();
        IssueSucceeds();

        await Issuer().Handle(IssueCmd(), default);

        _twoFactor.Verify(t => t.IssueAsync(
            _user, TwoFactorPurpose.StepUp, Op, null, "es", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── espera tras cambio de factor ────────────────────────────────────────

    [Fact]
    public async Task Reto_en_espera_tras_cambio_de_factor_no_emite_y_dice_hasta_cuando()
    {
        _user.SecurityFactorsChangedAt = FixedNow.AddHours(-1);

        var result = await Issuer().Handle(IssueCmd(), default);

        result.ErrorCode.Should().Be(StepUpGrant.CooldownErrorCode);
        result.Error.Should().Contain("2026-10-15T09:00:00");
        _twoFactor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reto_tras_la_espera_se_emite()
    {
        _user.SecurityFactorsChangedAt = FixedNow.AddHours(-24).AddSeconds(-1);
        IssueSucceeds();

        var result = await Issuer().Handle(IssueCmd(), default);

        result.IsSuccess.Should().BeTrue(because: result.Error);
    }

    [Fact]
    public async Task La_espera_se_configura_en_horas()
    {
        _config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:StepUp:FactorChangeCooldownHours"] = "2"
        }).Build();
        _user.SecurityFactorsChangedAt = FixedNow.AddHours(-3);
        IssueSucceeds();

        (await Issuer().Handle(IssueCmd(), default)).IsSuccess.Should().BeTrue();

        _user.SecurityFactorsChangedAt = FixedNow.AddHours(-1);
        (await Issuer().Handle(IssueCmd(), default)).ErrorCode.Should().Be(StepUpGrant.CooldownErrorCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("mucho")]
    public async Task Una_espera_mal_configurada_vuelve_a_24_horas_y_no_se_apaga(string configured)
    {
        _config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:StepUp:FactorChangeCooldownHours"] = configured
        }).Build();
        _user.SecurityFactorsChangedAt = FixedNow.AddHours(-23);

        var result = await Issuer().Handle(IssueCmd(), default);

        result.ErrorCode.Should().Be(StepUpGrant.CooldownErrorCode);
    }

    [Fact]
    public async Task Canje_en_espera_tras_cambio_de_factor_no_verifica_ni_da_permiso()
    {
        // El factor cambió entre la emisión y el canje: el reto ya está en manos de quien sea.
        _user.SecurityFactorsChangedAt = FixedNow.AddMinutes(-2);

        var result = await Verifier().Handle(VerifyCmd(), default);

        result.ErrorCode.Should().Be(StepUpGrant.CooldownErrorCode);
        _twoFactor.VerifyNoOtherCalls();
        _grants.VerifyNoOtherCalls();
    }

    // ── canje ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Codigo_bueno_devuelve_permiso_para_el_usuario_y_la_operacion()
    {
        VerifySucceeds();
        _grants.Setup(g => g.Issue(UserId, Op)).Returns("permiso");

        var result = await Verifier().Handle(VerifyCmd(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.StepUpToken.Should().Be("permiso");
    }

    [Fact]
    public async Task La_operacion_del_request_llega_a_la_verificacion()
    {
        VerifySucceeds();
        _grants.Setup(g => g.Issue(UserId, Op)).Returns("permiso");

        await Verifier().Handle(VerifyCmd(), default);

        // La operación del reto la comprueba la librería contra ESTA: si no llegara, un reto de
        // otra operación canjearía un permiso para esta.
        _twoFactor.Verify(t => t.VerifyAsync(
            "reto", "123456", TwoFactorPurpose.StepUp, Op, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Canje_para_usuario_inactivo_no_verifica_ni_da_permiso()
    {
        _user.IsActive = false;

        var result = await Verifier().Handle(VerifyCmd(), default);

        result.ErrorCode.Should().Be("USER_NOT_FOUND");
        _twoFactor.VerifyNoOtherCalls();
        _grants.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Canje_para_usuario_inexistente_no_verifica_ni_da_permiso()
    {
        var result = await Verifier(NoUsers()).Handle(VerifyCmd(), default);

        result.ErrorCode.Should().Be("USER_NOT_FOUND");
        _twoFactor.VerifyNoOtherCalls();
        _grants.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reto_de_otro_usuario_no_da_permiso()
    {
        _twoFactor.Setup(t => t.VerifyAsync("reto", "123456", TwoFactorPurpose.StepUp, Op, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<ChallengeClaims>.Success(Claims(userId: "otro")));

        var result = await Verifier().Handle(VerifyCmd(), default);

        result.ErrorCode.Should().Be("INVALID_CHALLENGE");
        _grants.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("CODE_INVALID")]
    [InlineData("INVALID_CHALLENGE")]
    [InlineData("TOO_MANY_ATTEMPTS")]
    public async Task Fallo_de_verificacion_se_propaga_y_no_da_permiso(string code)
    {
        _twoFactor.Setup(t => t.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), TwoFactorPurpose.StepUp, Op, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<ChallengeClaims>.Failure(code, "x"));

        var result = await Verifier().Handle(VerifyCmd(code: "000000"), default);

        result.ErrorCode.Should().Be(code);
        _grants.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Canje_para_operacion_desconocida_no_verifica_nada()
    {
        var result = await Verifier().Handle(VerifyCmd(op: "inventada", code: "1"), default);

        result.ErrorCode.Should().Be("UNKNOWN_OPERATION");
        _twoFactor.VerifyNoOtherCalls();
    }
}
