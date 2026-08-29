using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ResetPassword;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;
using System.Text;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

/// <summary>
/// Redención del enlace de recuperación.
/// </summary>
/// <remarks>
/// El DTO acepta <c>UserId</c> y <c>Email</c> y prefiere el primero. Son dos clientes con dos
/// contratos: el componente de SharedComponents postea <c>UserId</c> y la pantalla de BizCenterWeb
/// postea <c>Email</c>, que era lo único que este camino sabía leer. Aceptar los dos deja vivos a
/// los dos sin tener que cambiarlos a la vez, y el enlace del correo nuevo lleva <c>userId</c>
/// —una dirección en la query se queda en el historial, en los registros del proxy y en la
/// cabecera <c>Referer</c>—.
/// </remarks>
public class ResetPasswordHandlerTests
{
    private const string UserId      = "user-1";
    private const string Email       = "usuario@dominio.com";
    private const string OtherEmail  = "otro@dominio.com";
    private const string RawToken    = "raw+token/with=chars";
    private const string NewPassword = "P@ssw0rd!";

    private static readonly string EncodedToken =
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(RawToken));

    private static ApplicationUser User(string id = UserId, string email = Email, bool isActive = true) => new()
    {
        Id                 = id,
        Email              = email,
        IsActive           = isActive,
        RefreshToken       = "hash-de-un-refresco-vivo",
        RefreshTokenExpiry = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static Mock<UserManager<ApplicationUser>> Manager(
        ApplicationUser? byId = null, ApplicationUser? byEmail = null)
    {
        var m = UserManagerHelper.Create();
        m.Setup(x => x.FindByIdAsync(UserId)).ReturnsAsync(byId);
        m.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(byEmail);
        m.Setup(x => x.ResetPasswordAsync(It.IsAny<ApplicationUser>(), RawToken, NewPassword))
         .ReturnsAsync(IdentityResult.Success);
        m.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
        return m;
    }

    private static Task<MLMConquerorGlobalEdition.SharedKernel.Result<bool>> Run(
        Mock<UserManager<ApplicationUser>> manager, ResetPasswordRequest request)
        => new ResetPasswordHandler(manager.Object).Handle(
            new ResetPasswordCommand(request), CancellationToken.None);

    /// <summary>Lo que postea la pantalla de BizCenterWeb: solo Email. Tiene que seguir funcionando.</summary>
    [Fact]
    public async Task Handle_WhenOnlyEmailIsPosted_ResolvesTheAccountByEmail()
    {
        var user    = User();
        var manager = Manager(byEmail: user);

        var result = await Run(manager, new ResetPasswordRequest
        {
            Email       = Email,
            Token       = EncodedToken,
            NewPassword = NewPassword
        });

        result.IsSuccess.Should().BeTrue(because: result.Error);
        manager.Verify(x => x.FindByEmailAsync(Email), Times.Once);
        manager.Verify(x => x.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    /// <summary>Lo que postea el componente de SharedComponents y lo que trae el enlace del correo.</summary>
    [Fact]
    public async Task Handle_WhenOnlyUserIdIsPosted_ResolvesTheAccountById()
    {
        var user    = User();
        var manager = Manager(byId: user);

        var result = await Run(manager, new ResetPasswordRequest
        {
            UserId      = UserId,
            Token       = EncodedToken,
            NewPassword = NewPassword
        });

        result.IsSuccess.Should().BeTrue(because: result.Error);
        manager.Verify(x => x.FindByIdAsync(UserId), Times.Once);
        manager.Verify(x => x.FindByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    /// <summary>Si vienen los dos gana UserId: es el identificador del enlace.</summary>
    [Fact]
    public async Task Handle_WhenBothArePosted_PrefersUserId()
    {
        var byId    = User();
        var byEmail = User(id: "otro-usuario", email: OtherEmail);
        var manager = Manager(byId: byId, byEmail: byEmail);

        var result = await Run(manager, new ResetPasswordRequest
        {
            UserId      = UserId,
            Email       = Email,
            Token       = EncodedToken,
            NewPassword = NewPassword
        });

        result.IsSuccess.Should().BeTrue(because: result.Error);
        manager.Verify(x => x.ResetPasswordAsync(byId, RawToken, NewPassword), Times.Once);
        manager.Verify(x => x.FindByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// El enlace trae el token en base64url, igual que el de confirmación de dirección: el token
    /// crudo de Identity lleva '+', '/' y '=', que una query string corrompe. Aquí se decodifica.
    /// </summary>
    [Fact]
    public async Task Handle_DecodesTheBase64UrlTokenBeforeHandingItToIdentity()
    {
        var user    = User();
        var manager = Manager(byId: user);

        var result = await Run(manager, new ResetPasswordRequest
        {
            UserId      = UserId,
            Token       = EncodedToken,
            NewPassword = NewPassword
        });

        result.IsSuccess.Should().BeTrue(because: result.Error);
        manager.Verify(x => x.ResetPasswordAsync(user, RawToken, NewPassword), Times.Once);
    }

    /// <summary>Un enlace truncado es un error de dominio, no una FormatException que suba como 500.</summary>
    [Fact]
    public async Task Handle_WhenTokenIsNotBase64Url_ReturnsInvalidToken()
    {
        var manager = Manager(byId: User());

        var result = await Run(manager, new ResetPasswordRequest
        {
            UserId      = UserId,
            Token       = "no-es-base64url!!",
            NewPassword = NewPassword
        });

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_TOKEN");
    }

    /// <summary>
    /// Cambiar la contraseña tiene que tumbar las sesiones vivas: si no, quien tuviera el refresco
    /// robado seguiría dentro justo después de que la víctima creyera haber cerrado la puerta.
    /// </summary>
    [Fact]
    public async Task Handle_OnSuccess_InvalidatesRefreshTokens()
    {
        var user    = User();
        var manager = Manager(byId: user);

        var result = await Run(manager, new ResetPasswordRequest
        {
            UserId      = UserId,
            Token       = EncodedToken,
            NewPassword = NewPassword
        });

        result.IsSuccess.Should().BeTrue(because: result.Error);
        user.RefreshToken.Should().BeNull();
        user.RefreshTokenExpiry.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenAccountNotFound_ReturnsUserNotFound()
    {
        var manager = Manager();

        var result = await Run(manager, new ResetPasswordRequest
        {
            UserId      = UserId,
            Token       = EncodedToken,
            NewPassword = NewPassword
        });

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }
}
