using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Security;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.AccountEnrollment;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Phone;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.TwoFactorSettings;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

/// <summary>
/// Quién avisa al dueño y cuándo. El aviso es lo que da sentido a la espera del step-up: durante
/// esas horas el dueño tiene que enterarse de que alguien reapuntó un canal de sus códigos. Se
/// prueba también lo que NO avisa, por la misma razón que en <see cref="RevocacionDeSesionesTests"/>:
/// sin esas pruebas "aquí no se avisa" no se distingue de un olvido.
/// </summary>
public class AvisoDeCambioDeFactorTests
{
    private const string UserId = "user-001";

    private readonly Mock<ISecurityFactorNotifier>      _notifier    = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManager = UserManagerHelper.Create();

    private ApplicationUser Usuario(bool phoneConfirmed = false, TwoFactorChannel preferred = TwoFactorChannel.Email)
    {
        var user = new ApplicationUser
        {
            Id                        = UserId,
            Email                     = "usuario@dominio.com",
            IsActive                  = true,
            TwoFactorPhoneEncrypted   = "ENC",
            TwoFactorPhoneLast4       = "2671",
            TwoFactorPhoneConfirmed   = phoneConfirmed,
            PreferredTwoFactorChannel = preferred
        };
        _userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);
        _userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());
        return user;
    }

    private void Aviso(SecurityFactorChange change, Times times) =>
        _notifier.Verify(n => n.NotifyAsync(
            It.Is<ApplicationUser>(u => u.Id == UserId), change, null, It.IsAny<CancellationToken>()), times);

    private void NingunAviso() => _notifier.VerifyNoOtherCalls();

    // ── teléfono ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Confirmar_el_telefono_avisa()
    {
        Usuario();
        var twoFactor = new Mock<ITwoFactorService>();
        twoFactor.Setup(t => t.VerifyAsync("reto", "123456", TwoFactorPurpose.Enrollment, null, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<ChallengeClaims>.Success(new ChallengeClaims(
                     "jti", UserId, "usuario@dominio.com", TwoFactorPurpose.Enrollment, null,
                     TwoFactorChannel.Sms, "h", DateTime.Now, DateTime.Now.AddMinutes(5))));

        var result = await new VerifyPhoneHandler(_userManager.Object, twoFactor.Object, _notifier.Object)
            .Handle(new VerifyPhoneCommand(UserId, new VerifyPhoneRequest { ChallengeToken = "reto", Code = "123456" }), default);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        Aviso(SecurityFactorChange.Phone, Times.Once());
    }

    [Fact]
    public async Task Un_codigo_de_telefono_malo_no_avisa()
    {
        Usuario();
        var twoFactor = new Mock<ITwoFactorService>();
        twoFactor.Setup(t => t.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), TwoFactorPurpose.Enrollment, null, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<ChallengeClaims>.Failure("CODE_INVALID", "x"));

        await new VerifyPhoneHandler(_userManager.Object, twoFactor.Object, _notifier.Object)
            .Handle(new VerifyPhoneCommand(UserId, new VerifyPhoneRequest { ChallengeToken = "reto", Code = "000000" }), default);

        NingunAviso();
    }

    [Fact]
    public async Task Quitar_un_telefono_confirmado_avisa()
    {
        Usuario(phoneConfirmed: true);

        await new RemovePhoneHandler(_userManager.Object, _notifier.Object).Handle(new RemovePhoneCommand(UserId), default);

        Aviso(SecurityFactorChange.Phone, Times.Once());
    }

    [Fact]
    public async Task Quitar_un_telefono_que_nunca_se_confirmo_no_avisa()
    {
        Usuario(phoneConfirmed: false);

        await new RemovePhoneHandler(_userManager.Object, _notifier.Object).Handle(new RemovePhoneCommand(UserId), default);

        NingunAviso();
    }

    // ── canal ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cambiar_el_canal_avisa()
    {
        Usuario(phoneConfirmed: true, preferred: TwoFactorChannel.Email);

        var result = await new SetTwoFactorChannelHandler(_userManager.Object, _notifier.Object)
            .Handle(new SetTwoFactorChannelCommand(UserId, new SetTwoFactorChannelRequest { Channel = TwoFactorChannel.Sms }), default);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        Aviso(SecurityFactorChange.Channel, Times.Once());
    }

    [Fact]
    public async Task Elegir_el_canal_que_ya_se_tenia_no_avisa()
    {
        Usuario(preferred: TwoFactorChannel.Email);

        await new SetTwoFactorChannelHandler(_userManager.Object, _notifier.Object)
            .Handle(new SetTwoFactorChannelCommand(UserId, new SetTwoFactorChannelRequest { Channel = TwoFactorChannel.Email }), default);

        NingunAviso();
    }

    // ── autenticador ────────────────────────────────────────────────────────

    [Fact]
    public async Task Enrolar_un_autenticador_avisa()
    {
        var user = Usuario();
        var enrollment = new Mock<ITotpEnrollmentService>();
        enrollment.Setup(e => e.ConfirmAsync(user, "123456", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<bool>.Success(true));

        await new ConfirmAccountEnrollmentHandler(_userManager.Object, enrollment.Object, _notifier.Object)
            .Handle(new ConfirmAccountEnrollmentCommand(UserId, new ConfirmAccountEnrollmentRequest { Code = "123456" }), default);

        Aviso(SecurityFactorChange.Authenticator, Times.Once());
    }

    [Fact]
    public async Task Un_enrolamiento_con_codigo_malo_no_avisa()
    {
        var user = Usuario();
        var enrollment = new Mock<ITotpEnrollmentService>();
        enrollment.Setup(e => e.ConfirmAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<bool>.Failure("CODE_INVALID", "x"));

        await new ConfirmAccountEnrollmentHandler(_userManager.Object, enrollment.Object, _notifier.Object)
            .Handle(new ConfirmAccountEnrollmentCommand(UserId, new ConfirmAccountEnrollmentRequest { Code = "000000" }), default);

        NingunAviso();
    }

    [Fact]
    public async Task Apagar_el_segundo_factor_avisa()
    {
        var user = Usuario();
        var enrollment = new Mock<ITotpEnrollmentService>();
        enrollment.Setup(e => e.ResetAsync(user, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<bool>.Success(true));

        var result = await new DisableTwoFactorHandler(
                _userManager.Object, enrollment.Object, new ConfigurationBuilder().Build(), _notifier.Object)
            .Handle(new DisableTwoFactorCommand(UserId), default);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        Aviso(SecurityFactorChange.TwoFactorDisabled, Times.Once());
    }
}
