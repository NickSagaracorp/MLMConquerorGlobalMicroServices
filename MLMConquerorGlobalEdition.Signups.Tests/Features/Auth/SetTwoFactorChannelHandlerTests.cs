using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.TwoFactorSettings;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

/// <summary>
/// Cambio del canal preferido de 2FA.
/// </summary>
/// <remarks>
/// Lo que se prueba aquí es que <b>el servidor</b> rechaza un canal sin destino. Que la pantalla
/// solo ofrezca los canales de <c>AvailableChannels</c> es de presentación: quien llame a la API
/// directamente puede pedir SMS sin teléfono confirmado, y si el servidor lo aceptara, su
/// siguiente inicio de sesión mandaría el código a un canal que no existe y la cuenta se quedaría
/// fuera. Por eso hay una prueba de rechazo por cada canal, no solo de aceptación.
/// </remarks>
public class SetTwoFactorChannelHandlerTests
{
    private const string UserId = "user-001";
    private const string Email  = "usuario@dominio.com";

    private static readonly DateTime Enrolled = new(2026, 4, 1, 9, 30, 0, DateTimeKind.Utc);

    private static ApplicationUser User(
        bool      phoneConfirmed = false,
        bool      hasPhone       = false,
        DateTime? enrolledAt     = null) => new()
        {
            Id                        = UserId,
            Email                     = Email,
            IsActive                  = true,
            PreferredTwoFactorChannel = TwoFactorChannel.Email,
            TwoFactorPhoneEncrypted   = hasPhone ? "ENC:+14155552671" : null,
            TwoFactorPhoneLast4       = hasPhone ? "2671" : null,
            TwoFactorPhoneConfirmed   = phoneConfirmed,
            TwoFactorEnrolledAt       = enrolledAt
        };

    private static Mock<UserManager<ApplicationUser>> ManagerFor(ApplicationUser? user)
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);
        if (user is not null)
            userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        return userManager;
    }

    private static Task<MLMConquerorGlobalEdition.SharedKernel.Result<bool>> Run(
        Mock<UserManager<ApplicationUser>> userManager, TwoFactorChannel channel)
        => new SetTwoFactorChannelHandler(userManager.Object).Handle(
            new SetTwoFactorChannelCommand(UserId, new SetTwoFactorChannelRequest { Channel = channel }),
            CancellationToken.None);

    // ── aceptación: el canal tiene destino ───────────────────────────────────

    /// <summary>Correo siempre: es lo que identifica la cuenta, su destino existe por definición.</summary>
    [Fact]
    public async Task Handle_WhenChannelIsEmail_Accepts()
    {
        var user        = User();
        var userManager = ManagerFor(user);

        var result = await Run(userManager, TwoFactorChannel.Email);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        user.PreferredTwoFactorChannel.Should().Be(TwoFactorChannel.Email);
        userManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenChannelIsSmsAndPhoneConfirmed_Accepts()
    {
        var user        = User(hasPhone: true, phoneConfirmed: true);
        var userManager = ManagerFor(user);

        var result = await Run(userManager, TwoFactorChannel.Sms);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        user.PreferredTwoFactorChannel.Should().Be(TwoFactorChannel.Sms);
        userManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenChannelIsAuthenticatorAndEnrolled_Accepts()
    {
        var user        = User(enrolledAt: Enrolled);
        var userManager = ManagerFor(user);

        var result = await Run(userManager, TwoFactorChannel.Authenticator);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        user.PreferredTwoFactorChannel.Should().Be(TwoFactorChannel.Authenticator);
        userManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    // ── rechazo: el canal no tiene destino ───────────────────────────────────

    /// <summary>
    /// Un número que nadie ha demostrado tener no es un segundo factor. Aceptarlo dejaría al
    /// usuario esperando un SMS que la librería no llegaría a mandar.
    /// </summary>
    [Fact]
    public async Task Handle_WhenChannelIsSmsAndPhoneNotConfirmed_ReturnsChannelUnavailable()
    {
        var user        = User(hasPhone: true, phoneConfirmed: false);
        var userManager = ManagerFor(user);

        var result = await Run(userManager, TwoFactorChannel.Sms);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CHANNEL_UNAVAILABLE");
        user.PreferredTwoFactorChannel.Should().Be(TwoFactorChannel.Email);
        userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenChannelIsSmsAndNoPhoneAtAll_ReturnsChannelUnavailable()
    {
        var user        = User(hasPhone: false);
        var userManager = ManagerFor(user);

        var result = await Run(userManager, TwoFactorChannel.Sms);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CHANNEL_UNAVAILABLE");
        userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    /// <summary>
    /// Sin clave dada de alta no hay nada que Identity pueda verificar: la pantalla del código no
    /// aceptaría ninguno y el usuario no podría entrar.
    /// </summary>
    [Fact]
    public async Task Handle_WhenChannelIsAuthenticatorAndNotEnrolled_ReturnsChannelUnavailable()
    {
        var user        = User(enrolledAt: null);
        var userManager = ManagerFor(user);

        var result = await Run(userManager, TwoFactorChannel.Authenticator);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CHANNEL_UNAVAILABLE");
        user.PreferredTwoFactorChannel.Should().Be(TwoFactorChannel.Email);
        userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    /// <summary>
    /// Un entero que no pertenece al enum llega igual por el cable: el cuerpo es JSON y nadie
    /// obliga a quien llama a mandar uno de los tres valores. Cae por la misma comprobación —no
    /// está entre los canales con destino— sin necesitar una rama aparte.
    /// </summary>
    [Fact]
    public async Task Handle_WhenChannelIsNotAValidEnumValue_ReturnsChannelUnavailable()
    {
        var user        = User(hasPhone: true, phoneConfirmed: true, enrolledAt: Enrolled);
        var userManager = ManagerFor(user);

        var result = await Run(userManager, (TwoFactorChannel)99);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CHANNEL_UNAVAILABLE");
        user.PreferredTwoFactorChannel.Should().Be(TwoFactorChannel.Email);
        userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsUserNotFound()
    {
        var result = await Run(ManagerFor(null), TwoFactorChannel.Email);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }
}
