using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Phone;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

/// <summary>Baja del teléfono del 2FA.</summary>
public class RemovePhoneHandlerTests
{
    private const string UserId = "user-001";
    private const string Email  = "usuario@dominio.com";

    private static Mock<UserManager<ApplicationUser>> UserManagerWithUser(
        out ApplicationUser user,
        TwoFactorChannel    preferred = TwoFactorChannel.Email)
    {
        user = new ApplicationUser
        {
            Id                        = UserId,
            Email                     = Email,
            IsActive                  = true,
            PreferredTwoFactorChannel = preferred,
            TwoFactorPhoneEncrypted   = "ENC:+14155552671",
            TwoFactorPhoneLast4       = "2671",
            TwoFactorPhoneConfirmed   = true
        };
        var captured = user;

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(captured);
        userManager.Setup(m => m.UpdateAsync(captured)).ReturnsAsync(IdentityResult.Success);
        return userManager;
    }

    /// <summary>
    /// Los tres campos van juntos. Dejar el cifrado sin la marca —o al revés— deja un teléfono
    /// a medio borrar en la base de datos: PII que ya nadie usa y un estado que ningún camino
    /// del 2FA sabe leer.
    /// </summary>
    [Fact]
    public async Task RemovePhone_ClearsAllThreeFields()
    {
        var userManager = UserManagerWithUser(out var user);
        var handler     = new RemovePhoneHandler(userManager.Object);

        var result = await handler.Handle(new RemovePhoneCommand(UserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);

        user.TwoFactorPhoneEncrypted.Should().BeNull();
        user.TwoFactorPhoneLast4.Should().BeNull();
        user.TwoFactorPhoneConfirmed.Should().BeFalse();

        userManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    /// <summary>
    /// Quitar el teléfono con SMS como canal preferido dejaría al usuario con un canal sin
    /// destino: <c>ResolveTarget</c> devolvería null y su siguiente inicio de sesión terminaría
    /// en CHANNEL_UNAVAILABLE, sin código y sin manera de entrar. El correo siempre está —es el
    /// que identifica la cuenta—, así que ahí vuelve.
    /// </summary>
    [Fact]
    public async Task RemovePhone_WhenPreferredChannelWasSms_FallsBackToEmail()
    {
        var userManager = UserManagerWithUser(out var user, preferred: TwoFactorChannel.Sms);
        var handler     = new RemovePhoneHandler(userManager.Object);

        var result = await handler.Handle(new RemovePhoneCommand(UserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        user.PreferredTwoFactorChannel.Should().Be(TwoFactorChannel.Email);
    }

    /// <summary>
    /// Quien tenía la aplicación de autenticación como canal preferido la conserva: su segundo
    /// factor no depende del teléfono que se está borrando.
    /// </summary>
    [Fact]
    public async Task RemovePhone_WhenPreferredChannelWasAuthenticator_KeepsIt()
    {
        var userManager = UserManagerWithUser(out var user, preferred: TwoFactorChannel.Authenticator);
        var handler     = new RemovePhoneHandler(userManager.Object);

        var result = await handler.Handle(new RemovePhoneCommand(UserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        user.PreferredTwoFactorChannel.Should().Be(TwoFactorChannel.Authenticator);
    }

    [Fact]
    public async Task RemovePhone_WhenUserNotFound_ReturnsUserNotFound()
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        var handler = new RemovePhoneHandler(userManager.Object);

        var result = await handler.Handle(new RemovePhoneCommand(UserId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }
}
