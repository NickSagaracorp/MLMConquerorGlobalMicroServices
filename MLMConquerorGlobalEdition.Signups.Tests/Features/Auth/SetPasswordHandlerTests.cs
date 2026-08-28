using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.SetPassword;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

/// <summary>
/// Fija la primera contraseña de una cuenta que no tiene ninguna. Hoy no hay logins externos, así
/// que ninguna cuenta real llega sin contraseña: el endpoint está construido para el día que se
/// acepte entrar con Google o Microsoft, donde la cuenta nace sin contraseña local.
///
/// El usuario sale siempre del token: el comando solo conoce un UserId que el controlador saca de
/// las claims, nunca del cuerpo.
/// </summary>
public class SetPasswordHandlerTests
{
    private const string UserId      = "user-001";
    private const string NewPassword = "Contrasena1!";

    private static ApplicationUser User(bool active = true) => new()
    {
        Id                 = UserId,
        Email              = "usuario@dominio.com",
        IsActive           = active,
        RefreshToken       = "REFRESH-VIEJO",
        RefreshTokenExpiry = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static Mock<UserManager<ApplicationUser>> UserManagerWith(
        ApplicationUser?  user,
        bool              hasPassword,
        IdentityResult?   addResult = null)
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);
        userManager.Setup(m => m.HasPasswordAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(hasPassword);
        userManager.Setup(m => m.AddPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                   .ReturnsAsync(addResult ?? IdentityResult.Success);
        userManager.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
        return userManager;
    }

    private static SetPasswordCommand Command(string password = NewPassword) =>
        new(UserId, new SetPasswordRequest { NewPassword = password });

    /// <summary>
    /// Sin contraseña previa se fija, y con <c>AddPasswordAsync</c>: <c>ChangePasswordAsync</c>
    /// exige la actual, que en este escenario no existe.
    /// </summary>
    [Fact]
    public async Task SetPassword_WhenAccountHasNoPassword_AddsIt()
    {
        var user        = User();
        var userManager = UserManagerWith(user, hasPassword: false);
        var handler     = new SetPasswordHandler(userManager.Object);

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);

        userManager.Verify(m => m.AddPasswordAsync(user, NewPassword), Times.Once);
        userManager.Verify(m => m.ChangePasswordAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);

        // Mismo criterio que el cambio de contraseña: las sesiones abiertas antes de que la cuenta
        // tuviera credencial no la sobreviven.
        user.RefreshToken.Should().BeNull();
        user.RefreshTokenExpiry.Should().BeNull();
        userManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    /// <summary>
    /// Con contraseña ya puesta esto sería fijar una nueva sin demostrar la anterior: quien se
    /// hiciera con una sesión ajena se quedaría la cuenta sin conocerla. El código propio dirige
    /// a cambiarla, que es el camino que sí pide la actual.
    /// </summary>
    [Fact]
    public async Task SetPassword_WhenAccountAlreadyHasPassword_FailsWithPasswordAlreadySet()
    {
        var user        = User();
        var userManager = UserManagerWith(user, hasPassword: true);
        var handler     = new SetPasswordHandler(userManager.Object);

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PASSWORD_ALREADY_SET");

        userManager.Verify(m => m.AddPasswordAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);

        // La cuenta se queda exactamente como estaba.
        user.RefreshToken.Should().Be("REFRESH-VIEJO");
        userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    /// <summary>
    /// La política de contraseñas la aplica Identity y sus descripciones se propagan tal cual:
    /// el usuario tiene que poder leer qué le falta a la que escribió.
    /// </summary>
    [Fact]
    public async Task SetPassword_WhenPasswordViolatesPolicy_ReturnsIdentityErrors()
    {
        var failure = IdentityResult.Failed(
            new IdentityError { Code = "PasswordTooShort", Description = "Passwords must be at least 8 characters." },
            new IdentityError { Code = "PasswordRequiresDigit", Description = "Passwords must have at least one digit." });

        var user        = User();
        var userManager = UserManagerWith(user, hasPassword: false, addResult: failure);
        var handler     = new SetPasswordHandler(userManager.Object);

        var result = await handler.Handle(Command("abc"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PASSWORD_SET_FAILED");
        result.Error.Should().Contain("at least 8 characters");
        result.Error.Should().Contain("at least one digit");

        // Sin contraseña puesta no se tocan los tokens de refresco.
        userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task SetPassword_WhenUserNotFound_ReturnsUserNotFound()
    {
        var userManager = UserManagerWith(null, hasPassword: false);
        var handler     = new SetPasswordHandler(userManager.Object);

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
        userManager.Verify(m => m.AddPasswordAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SetPassword_WhenUserInactive_ReturnsUserNotFound()
    {
        var userManager = UserManagerWith(User(active: false), hasPassword: false);
        var handler     = new SetPasswordHandler(userManager.Object);

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }
}
