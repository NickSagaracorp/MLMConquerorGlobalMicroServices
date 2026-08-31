using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ForgotPassword;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;
using System.Text;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

/// <summary>
/// Correo de recuperación de contraseña. Hasta ahora el handler generaba el token y lo tiraba con
/// un TODO: nadie ha recuperado nunca su contraseña por su cuenta.
/// </summary>
/// <remarks>
/// La respuesta es <b>idéntica</b> exista o no la cuenta — y también si el transporte revienta.
/// Un endpoint que distingue esos casos es un oráculo de enumeración: se le prueba una lista de
/// correos y las respuestas dicen cuáles están registrados. Mismo criterio que
/// <c>SendEmailConfirmationHandler</c>.
/// </remarks>
public class ForgotPasswordHandlerTests
{
    private const string Email    = "usuario@dominio.com";
    private const string RawToken = "raw+token/with=chars";

    private static IConfiguration Config() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:PortalBaseUrl"]      = "https://portal.test",
                ["Auth:AdminPortalBaseUrl"] = "https://admin.test"
            })
            .Build();

    private static ForgotPasswordHandler BuildHandler(
        Mock<UserManager<ApplicationUser>> userManager,
        Mock<IEmailService>                email,
        AppDbContext?                      db = null)
        => new(userManager.Object, email.Object, db ?? InMemoryDbHelper.Create(), Config(),
               Mock.Of<ILogger<ForgotPasswordHandler>>());

    private static Mock<UserManager<ApplicationUser>> UserManagerWith(
        ApplicationUser? user, string token = RawToken)
    {
        var m = UserManagerHelper.Create();
        m.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        m.Setup(x => x.GeneratePasswordResetTokenAsync(It.IsAny<ApplicationUser>()))
         .ReturnsAsync(token);
        return m;
    }

    private static ApplicationUser Member(bool isActive = true) => new()
    {
        Id              = "user-1",
        Email           = Email,
        IsActive        = isActive,
        MemberProfileId = "AMB-000001"
    };

    private static ApplicationUser Staff() => new()
    {
        Id              = "staff-1",
        Email           = Email,
        IsActive        = true,
        MemberProfileId = null
    };

    [Fact]
    public async Task Handle_WhenUserExists_SendsEmailWithBase64UrlTokenAndUserId()
    {
        var userManager = UserManagerWith(Member());
        var email       = new Mock<IEmailService>();

        var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(new MemberProfile
        {
            MemberId        = "AMB-000001",
            FirstName       = "Ana",
            LastName        = "Ruiz",
            DefaultLanguage = "es"
        });
        await db.SaveChangesAsync();

        var handler = BuildHandler(userManager, email, db);

        var result = await handler.Handle(new ForgotPasswordCommand(Email), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // El token de Identity lleva '+', '/' y '=' — caracteres que una query string corrompe
        // ('+' se decodifica como espacio). Viaja en base64url o el enlace funcionaría en unas
        // cuentas y en otras no, según qué caracteres salieran en el token.
        var expectedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(RawToken));

        email.Verify(e => e.SendAsync(
            Email,
            It.IsAny<string>(),
            "es",
            NotificationEvents.PasswordReset,
            It.Is<Dictionary<string, string>>(v =>
                v.ContainsKey("ResetUrl")
                && v["ResetUrl"].Contains(expectedToken)
                && v["ResetUrl"].StartsWith("https://portal.test/auth/reset-password")
                // userId y no email: la dirección en la query se queda en el historial del
                // navegador, en los registros del proxy y en la cabecera Referer.
                && v["ResetUrl"].Contains("userId=user-1")
                && !v["ResetUrl"].Contains("email=")
                && !v["ResetUrl"].Contains('+')
                && v.ContainsKey("ExpiresInMinutes") && v["ExpiresInMinutes"] == "15"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// El staff no tiene <c>MemberProfileId</c>: su enlace apunta al portal de administración.
    /// Mandarlo al portal equivocado lo deja en una pantalla de inicio de sesión que no es la suya.
    /// </summary>
    [Fact]
    public async Task Handle_WhenUserIsStaff_UsesAdminPortalBaseUrlAndDefaultLanguage()
    {
        var userManager = UserManagerWith(Staff());
        var email       = new Mock<IEmailService>();
        var handler     = BuildHandler(userManager, email);

        var result = await handler.Handle(new ForgotPasswordCommand(Email), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        email.Verify(e => e.SendAsync(
            Email,
            It.IsAny<string>(),
            "en",
            NotificationEvents.PasswordReset,
            It.Is<Dictionary<string, string>>(v =>
                v["ResetUrl"].StartsWith("https://admin.test/auth/reset-password")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ReturnsSuccessWithoutSending()
    {
        var userManager = UserManagerWith(null);
        var email       = new Mock<IEmailService>();
        var handler     = BuildHandler(userManager, email);

        var result = await handler.Handle(new ForgotPasswordCommand(Email), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        email.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserIsInactive_ReturnsSuccessWithoutSending()
    {
        var userManager = UserManagerWith(Member(isActive: false));
        var email       = new Mock<IEmailService>();
        var handler     = BuildHandler(userManager, email);

        var result = await handler.Handle(new ForgotPasswordCommand(Email), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        email.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Que el transporte falle no puede cambiar la respuesta. Una excepción que solo se produce
    /// cuando la cuenta existe reintroduce por la puerta de atrás el mismo oráculo de enumeración
    /// que este endpoint evita: bastaría con mirar cuáles de los correos probados devuelven 500.
    /// </summary>
    [Fact]
    public async Task Handle_WhenEmailTransportThrows_StillReturnsSuccess()
    {
        var userManager = UserManagerWith(Member());
        var email       = new Mock<IEmailService>();

        email.Setup(e => e.SendAsync(
                 It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                 It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("No hay plantilla sembrada."));

        var handler = BuildHandler(userManager, email);

        var result = await handler.Handle(new ForgotPasswordCommand(Email), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
