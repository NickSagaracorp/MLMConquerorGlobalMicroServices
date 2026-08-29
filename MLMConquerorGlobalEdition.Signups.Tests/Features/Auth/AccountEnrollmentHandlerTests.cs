using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Authn.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.AccountEnrollment;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

/// <summary>
/// Enrolamiento del autenticador <b>desde una sesión ya iniciada</b>.
/// </summary>
/// <remarks>
/// Es el hueco que dejaba el enrolamiento por token: aquel exige un <c>EnrollmentToken</c> que
/// solo emite el login cuando fuerza el enrolamiento, así que un usuario que ya entró no podía
/// activar ni volver a enrolar su autenticador. Aquí el usuario sale del token de acceso.
///
/// Las pruebas usan el <see cref="TotpEnrollmentService"/> de verdad y no un doble: lo que hay que
/// comprobar —que dos llamadas seguidas devuelven la misma clave y que un usuario ya enrolado
/// recibe una nueva— es precisamente el comportamiento de ese servicio, y con un doble solo se
/// comprobaría que el handler lo llama.
/// </remarks>
public class AccountEnrollmentHandlerTests
{
    private const string UserId = "user-001";
    private const string Email  = "usuario@dominio.com";

    private const string FirstKey  = "JBSWY3DPEHPK3PXP";
    private const string SecondKey = "MZXW6YTBOI======";

    private static readonly DateTime FixedNow = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Enrolled = new(2026, 4, 1, 9, 30, 0, DateTimeKind.Utc);

    private static ApplicationUser User(DateTime? enrolledAt = null) => new()
    {
        Id                        = UserId,
        Email                     = Email,
        IsActive                  = true,
        PreferredTwoFactorChannel = TwoFactorChannel.Email,
        TwoFactorEnrolledAt       = enrolledAt
    };

    /// <summary>
    /// Imita el almacén de claves de Identity: <c>GetAuthenticatorKeyAsync</c> devuelve lo que
    /// haya en ese momento y <c>ResetAuthenticatorKeyAsync</c> lo sustituye. Con un Setup fijo no
    /// se podría distinguir "reutilizó la clave" de "generó otra igual", que es justo lo que estas
    /// pruebas tienen que separar.
    /// </summary>
    private static void WireKeyStore(
        Mock<UserManager<ApplicationUser>> userManager,
        ApplicationUser                    user,
        string?                            initialKey,
        params string[]                    keysAfterReset)
    {
        var current = initialKey;
        var next    = 0;

        userManager.Setup(m => m.GetAuthenticatorKeyAsync(user)).ReturnsAsync(() => current);
        userManager
            .Setup(m => m.ResetAuthenticatorKeyAsync(user))
            .Callback(() =>
            {
                current = keysAfterReset.Length == 0
                    ? null
                    : keysAfterReset[Math.Min(next++, keysAfterReset.Length - 1)];
            })
            .ReturnsAsync(IdentityResult.Success);
    }

    private static Mock<UserManager<ApplicationUser>> ManagerFor(ApplicationUser? user)
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);
        if (user is not null)
            userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        return userManager;
    }

    private static TotpEnrollmentService RealEnrollment(Mock<UserManager<ApplicationUser>> userManager)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(FixedNow);
        clock.Setup(c => c.UtcNow).Returns(FixedNow);

        return new TotpEnrollmentService(
            userManager.Object, clock.Object, new ConfigurationBuilder().Build());
    }

    // ── begin ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Begin_ReturnsSharedKeyUriAndQr()
    {
        var user        = User();
        var userManager = ManagerFor(user);
        WireKeyStore(userManager, user, initialKey: FirstKey);

        var handler = new BeginAccountEnrollmentHandler(userManager.Object, RealEnrollment(userManager));

        var result = await handler.Handle(new BeginAccountEnrollmentCommand(UserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.SharedKey.Should().Be(FirstKey);
        result.Value.AuthenticatorUri.Should().StartWith("otpauth://totp/");
        result.Value.QrCodePngDataUri.Should().StartWith("data:image/png;base64,");
    }

    /// <summary>
    /// Idempotente mientras el enrolamiento sigue abierto. Si cada llamada regenerase la clave, el
    /// QR que el usuario acaba de escanear moriría al recargar la página tras un código erróneo y
    /// volvería a teclear el número de una entrada ya inválida, fallando sin entender por qué.
    /// </summary>
    [Fact]
    public async Task Begin_CalledTwiceWhileEnrollmentIsOpen_ReturnsTheSameKey()
    {
        var user        = User(enrolledAt: null);
        var userManager = ManagerFor(user);
        WireKeyStore(userManager, user, initialKey: FirstKey, keysAfterReset: SecondKey);

        var handler = new BeginAccountEnrollmentHandler(userManager.Object, RealEnrollment(userManager));

        var first  = await handler.Handle(new BeginAccountEnrollmentCommand(UserId), CancellationToken.None);
        var second = await handler.Handle(new BeginAccountEnrollmentCommand(UserId), CancellationToken.None);

        first.IsSuccess.Should().BeTrue(because: first.Error);
        second.IsSuccess.Should().BeTrue(because: second.Error);
        second.Value!.SharedKey.Should().Be(first.Value!.SharedKey).And.Be(FirstKey);

        userManager.Verify(m => m.ResetAuthenticatorKeyAsync(user), Times.Never);
    }

    /// <summary>
    /// Re-enrolar. Devolver la clave que el usuario ya usa dejaría viva la entrada vieja de su
    /// aplicación y no habría cambiado nada — que es justo lo que se busca al re-enrolar.
    /// </summary>
    [Fact]
    public async Task Begin_WhenAlreadyEnrolled_ReturnsANewKey()
    {
        var user        = User(enrolledAt: Enrolled);
        var userManager = ManagerFor(user);
        WireKeyStore(userManager, user, initialKey: FirstKey, keysAfterReset: SecondKey);

        var handler = new BeginAccountEnrollmentHandler(userManager.Object, RealEnrollment(userManager));

        var result = await handler.Handle(new BeginAccountEnrollmentCommand(UserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.SharedKey.Should().Be(SecondKey).And.NotBe(FirstKey);

        userManager.Verify(m => m.ResetAuthenticatorKeyAsync(user), Times.Once);
    }

    [Fact]
    public async Task Begin_WhenUserNotFound_ReturnsUserNotFound()
    {
        var userManager = ManagerFor(null);
        var handler     = new BeginAccountEnrollmentHandler(userManager.Object, RealEnrollment(userManager));

        var result = await handler.Handle(new BeginAccountEnrollmentCommand(UserId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }

    // ── confirm ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Confirm_WhenCodeIsValid_EnablesTwoFactor()
    {
        var user        = User();
        var userManager = ManagerFor(user);

        userManager.Setup(m => m.VerifyTwoFactorTokenAsync(
                       user, TokenOptions.DefaultAuthenticatorProvider, "123456"))
                   .ReturnsAsync(true);
        userManager.Setup(m => m.SetTwoFactorEnabledAsync(user, true))
                   .Callback(() => user.TwoFactorEnabled = true)
                   .ReturnsAsync(IdentityResult.Success);

        var handler = new ConfirmAccountEnrollmentHandler(userManager.Object, RealEnrollment(userManager));

        var result = await handler.Handle(
            new ConfirmAccountEnrollmentCommand(UserId, new ConfirmAccountEnrollmentRequest { Code = "123456" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value.Should().BeTrue();

        user.TwoFactorEnabled.Should().BeTrue();
        user.TwoFactorEnrolledAt.Should().Be(FixedNow);
        user.PreferredTwoFactorChannel.Should().Be(TwoFactorChannel.Authenticator);
    }

    [Fact]
    public async Task Confirm_WhenCodeIsInvalid_DoesNotEnableTwoFactor()
    {
        var user        = User();
        var userManager = ManagerFor(user);

        userManager.Setup(m => m.VerifyTwoFactorTokenAsync(
                       user, TokenOptions.DefaultAuthenticatorProvider, "000000"))
                   .ReturnsAsync(false);

        var handler = new ConfirmAccountEnrollmentHandler(userManager.Object, RealEnrollment(userManager));

        var result = await handler.Handle(
            new ConfirmAccountEnrollmentCommand(UserId, new ConfirmAccountEnrollmentRequest { Code = "000000" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CODE_INVALID");

        user.TwoFactorEnabled.Should().BeFalse();
        user.TwoFactorEnrolledAt.Should().BeNull();

        userManager.Verify(m => m.SetTwoFactorEnabledAsync(It.IsAny<ApplicationUser>(), It.IsAny<bool>()),
                           Times.Never);
    }

    [Fact]
    public async Task Confirm_WhenUserNotFound_ReturnsUserNotFound()
    {
        var userManager = ManagerFor(null);
        var handler     = new ConfirmAccountEnrollmentHandler(userManager.Object, RealEnrollment(userManager));

        var result = await handler.Handle(
            new ConfirmAccountEnrollmentCommand(UserId, new ConfirmAccountEnrollmentRequest { Code = "123456" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }
}
