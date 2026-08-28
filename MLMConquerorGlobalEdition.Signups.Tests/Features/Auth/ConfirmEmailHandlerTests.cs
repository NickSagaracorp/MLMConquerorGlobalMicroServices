using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.EmailConfirmation;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;
using System.Text;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

/// <summary>
/// A diferencia del envío, aquí <b>sí</b> se puede fallar explícitamente: quien llega con un
/// userId y un token ya tiene el enlace en la mano, no está sondeando qué correos existen.
/// </summary>
public class ConfirmEmailHandlerTests
{
    private const string RawToken = "raw+token/with=chars";

    private static readonly string EncodedToken =
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(RawToken));

    private static ApplicationUser User(bool confirmed = false) => new()
    {
        Id             = "user-1",
        Email          = "nuevo@test.com",
        EmailConfirmed = confirmed,
        IsActive       = true
    };

    private static ConfirmEmailCommand Command(string? userId = null, string? token = null)
        => new(new ConfirmEmailRequest
        {
            UserId = userId ?? "user-1",
            Token  = token  ?? EncodedToken
        });

    [Fact]
    public async Task Handle_WhenTokenValid_ConfirmsEmail()
    {
        var user        = User();
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(user);
        userManager.Setup(m => m.ConfirmEmailAsync(user, It.IsAny<string>()))
                   .ReturnsAsync(IdentityResult.Success);

        var handler = new ConfirmEmailHandler(userManager.Object);

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // El token llega en base64url y se decodifica antes de dárselo a Identity: si se le
        // pasara tal cual, ConfirmEmailAsync rechazaría todos los enlaces.
        userManager.Verify(m => m.ConfirmEmailAsync(user, RawToken), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTokenInvalid_Fails()
    {
        var user        = User();
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(user);
        userManager.Setup(m => m.ConfirmEmailAsync(user, It.IsAny<string>()))
                   .ReturnsAsync(IdentityResult.Failed(new IdentityError
                   {
                       Code        = "InvalidToken",
                       Description = "Invalid token."
                   }));

        var handler = new ConfirmEmailHandler(userManager.Object);

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("EMAIL_CONFIRMATION_FAILED");
        user.EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_Fails()
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync(It.IsAny<string>()))
                   .ReturnsAsync((ApplicationUser?)null);

        var handler = new ConfirmEmailHandler(userManager.Object);

        var result = await handler.Handle(Command(userId: "desconocido"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }

    /// <summary>
    /// Idempotente: reabrir el enlace del correo —o que el antivirus del cliente lo visite
    /// antes que el usuario— no debe dar error. El token de Identity ya se consumió y
    /// <c>ConfirmEmailAsync</c> lo rechazaría, así que hay que cortar antes de llamarlo.
    /// </summary>
    [Fact]
    public async Task Handle_WhenAlreadyConfirmed_ReturnsSuccess()
    {
        var user        = User(confirmed: true);
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(user);

        var handler = new ConfirmEmailHandler(userManager.Object);

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        userManager.Verify(m => m.ConfirmEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    /// <summary>
    /// Un token que no es base64url válido (enlace truncado por el cliente de correo, por
    /// ejemplo) tiene que devolver un error de dominio y no una <c>FormatException</c> que
    /// suba como 500.
    /// </summary>
    [Fact]
    public async Task Handle_WhenTokenIsNotBase64Url_FailsWithInvalidToken()
    {
        var user        = User();
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(user);

        var handler = new ConfirmEmailHandler(userManager.Object);

        var result = await handler.Handle(Command(token: "no-es-base64url!!!"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_TOKEN");

        userManager.Verify(m => m.ConfirmEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }
}
