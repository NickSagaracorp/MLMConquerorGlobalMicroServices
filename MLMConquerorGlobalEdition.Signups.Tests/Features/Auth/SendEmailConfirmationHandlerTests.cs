using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.EmailConfirmation;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;
using System.Text;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

/// <summary>
/// El endpoint de envío responde <b>idéntico</b> exista o no la cuenta, esté o no confirmada.
/// Un endpoint que distingue esos casos es un oráculo de enumeración: se le prueba una lista de
/// correos y las respuestas dicen cuáles están registrados. Es el mismo criterio que ya aplica
/// <c>ForgotPasswordHandler</c>.
/// </summary>
public class SendEmailConfirmationHandlerTests
{
    private const string Email = "nuevo@test.com";

    private static IConfiguration Config() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:PortalBaseUrl"]      = "https://portal.test",
                ["Auth:AdminPortalBaseUrl"] = "https://admin.test"
            })
            .Build();

    private static SendEmailConfirmationHandler BuildHandler(
        Mock<UserManager<ApplicationUser>> userManager,
        Mock<IEmailService>                email,
        AppDbContext?                      db = null)
        => new(userManager.Object, email.Object, db ?? InMemoryDbHelper.Create(), Config(),
               Mock.Of<ILogger<SendEmailConfirmationHandler>>());

    private static Mock<UserManager<ApplicationUser>> UserManagerWith(
        ApplicationUser? user, string token = "raw+token/with=chars")
    {
        var m = UserManagerHelper.Create();
        m.Setup(x => x.FindByEmailAsync(Email)).ReturnsAsync(user);
        m.Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
         .ReturnsAsync(token);
        return m;
    }

    private static ApplicationUser Member(bool confirmed = false) => new()
    {
        Id              = "user-1",
        Email           = Email,
        EmailConfirmed  = confirmed,
        IsActive        = true,
        MemberProfileId = "AMB-000001"
    };

    [Fact]
    public async Task Handle_WhenUserExistsAndNotConfirmed_SendsEmail()
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

        var result = await handler.Handle(new SendEmailConfirmationCommand(Email), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        userManager.Verify(x => x.GenerateEmailConfirmationTokenAsync(
            It.Is<ApplicationUser>(u => u.Id == "user-1")), Times.Once);

        // El token de Identity lleva '+', '/' y '=' — caracteres que una query string corrompe.
        // Viaja en base64url; el enlace del correo tiene que contener el token ya codificado y
        // no el crudo, o el enlace funcionará en unas cuentas y fallará en otras según qué
        // caracteres salgan en el token.
        var expectedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("raw+token/with=chars"));

        email.Verify(e => e.SendAsync(
            Email,
            It.IsAny<string>(),
            "es",
            NotificationEvents.EmailConfirmation,
            It.Is<Dictionary<string, string>>(v =>
                v.ContainsKey("ConfirmationUrl")
                && v["ConfirmationUrl"].Contains(expectedToken)
                && v["ConfirmationUrl"].StartsWith("https://portal.test")
                && !v["ConfirmationUrl"].Contains('+')
                && v.ContainsKey("ExpiresInMinutes") && v["ExpiresInMinutes"] == "15"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserAlreadyConfirmed_ReturnsSuccessWithoutSending()
    {
        var userManager = UserManagerWith(Member(confirmed: true));
        var email       = new Mock<IEmailService>();
        var handler     = BuildHandler(userManager, email);

        var result = await handler.Handle(new SendEmailConfirmationCommand(Email), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        email.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ReturnsSuccessWithoutSending()
    {
        var userManager = UserManagerWith(null);
        var email       = new Mock<IEmailService>();
        var handler     = BuildHandler(userManager, email);

        var result = await handler.Handle(new SendEmailConfirmationCommand(Email), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        email.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// El staff no tiene <c>MemberProfileId</c>: su enlace apunta al portal de administración,
    /// no al del BizCenter. Mandarlo al portal equivocado deja al usuario en una pantalla de
    /// inicio de sesión que no es la suya.
    /// </summary>
    [Fact]
    public async Task Handle_WhenUserIsStaff_UsesAdminPortalBaseUrl()
    {
        var staff = new ApplicationUser
        {
            Id              = "staff-1",
            Email           = Email,
            EmailConfirmed  = false,
            IsActive        = true,
            MemberProfileId = null
        };

        var userManager = UserManagerWith(staff);
        var email       = new Mock<IEmailService>();
        var handler     = BuildHandler(userManager, email);

        await handler.Handle(new SendEmailConfirmationCommand(Email), CancellationToken.None);

        email.Verify(e => e.SendAsync(
            Email, It.IsAny<string>(), "en", NotificationEvents.EmailConfirmation,
            It.Is<Dictionary<string, string>>(v => v["ConfirmationUrl"].StartsWith("https://admin.test")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Si el transporte de correo revienta, la respuesta tiene que seguir siendo la misma: una
    /// excepción que solo se produce cuando la cuenta existe reintroduce por la puerta de atrás
    /// exactamente el oráculo de enumeración que este endpoint evita.
    /// </summary>
    [Fact]
    public async Task Handle_WhenEmailTransportThrows_StillReturnsSuccess()
    {
        var userManager = UserManagerWith(Member());
        var email       = new Mock<IEmailService>();
        email.Setup(e => e.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("SES down"));

        var handler = BuildHandler(userManager, email);

        var result = await handler.Handle(new SendEmailConfirmationCommand(Email), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
