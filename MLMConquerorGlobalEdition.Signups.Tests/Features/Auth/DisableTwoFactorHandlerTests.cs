using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Services;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.TwoFactorSettings;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

/// <summary>
/// Desactivación del 2FA.
/// </summary>
/// <remarks>
/// Dos cosas se prueban aquí, y las dos son del servidor. Que la pantalla esconda el botón cuando
/// el rol obliga a llevar segundo factor no cierra la ruta: una llamada directa la alcanza igual,
/// y al otro lado está la política que obliga al personal con acceso al panel. Y que al desactivar
/// se reinicie la clave del autenticador, porque dejarla viva significa que la entrada del
/// teléfono del usuario —quizá en un aparato que ya no controla— seguiría valiendo en cuanto
/// volviera a activarlo.
/// </remarks>
public class DisableTwoFactorHandlerTests
{
    private const string UserId = "user-001";
    private const string Email  = "usuario@dominio.com";

    private static readonly DateTime FixedNow = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Enrolled = new(2026, 4, 1, 9, 30, 0, DateTimeKind.Utc);

    private static ApplicationUser User() => new()
    {
        Id                  = UserId,
        Email               = Email,
        IsActive            = true,
        TwoFactorEnabled    = true,
        TwoFactorEnrolledAt = Enrolled
    };

    private static IConfiguration Config(params string[] mandatoryRoles)
    {
        var data = new Dictionary<string, string?>();
        for (var i = 0; i < mandatoryRoles.Length; i++)
            data[$"Auth:TwoFactor:MandatoryRoles:{i}"] = mandatoryRoles[i];

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    private static Mock<UserManager<ApplicationUser>> ManagerFor(
        ApplicationUser? user, params string[] roles)
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);
        if (user is not null)
        {
            userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(roles);
            userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        }
        return userManager;
    }

    private static Mock<ITotpEnrollmentService> EnrollmentService()
    {
        var m = new Mock<ITotpEnrollmentService>();
        m.Setup(s => s.ResetAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<bool>.Success(true));
        return m;
    }

    private static Task<Result<bool>> Run(
        Mock<UserManager<ApplicationUser>> userManager,
        Mock<ITotpEnrollmentService>       enrollment,
        IConfiguration                     config)
        => new DisableTwoFactorHandler(userManager.Object, enrollment.Object, config)
            .Handle(new DisableTwoFactorCommand(UserId), CancellationToken.None);

    [Fact]
    public async Task Handle_WhenRoleDoesNotRequireTwoFactor_Disables()
    {
        var user        = User();
        var userManager = ManagerFor(user, "Ambassador");
        var enrollment  = EnrollmentService();

        var result = await Run(userManager, enrollment, Config("Admin"));

        result.IsSuccess.Should().BeTrue(because: result.Error);
        enrollment.Verify(s => s.ResetAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoRoleIsMandatory_Disables()
    {
        var user        = User();
        var userManager = ManagerFor(user, "Admin");
        var enrollment  = EnrollmentService();

        var result = await Run(userManager, enrollment, Config());

        result.IsSuccess.Should().BeTrue(because: result.Error);
        enrollment.Verify(s => s.ResetAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// La comprobación es del servidor, no de la pantalla: si el rol obliga, la llamada se rechaza
    /// venga de donde venga, y el 2FA no se toca.
    /// </summary>
    [Fact]
    public async Task Handle_WhenRoleRequiresTwoFactor_ReturnsTwoFactorRequired()
    {
        var user        = User();
        var userManager = ManagerFor(user, "Admin");
        var enrollment  = EnrollmentService();

        var result = await Run(userManager, enrollment, Config("Admin", "Support"));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TWO_FACTOR_REQUIRED");

        enrollment.Verify(s => s.ResetAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()),
                          Times.Never);
        user.TwoFactorEnabled.Should().BeTrue();
        user.TwoFactorEnrolledAt.Should().Be(Enrolled);
    }

    /// <summary>
    /// La lista la escribe una persona en la configuración: "admin" tiene que valer lo mismo que
    /// "Admin", o la política dejaría de aplicarse por una mayúscula. Mismo criterio que
    /// <c>LoginHandler</c>, que lee esa misma lista para forzar el enrolamiento.
    /// </summary>
    [Fact]
    public async Task Handle_MatchesMandatoryRolesIgnoringCase()
    {
        var user        = User();
        var userManager = ManagerFor(user, "Admin");
        var enrollment  = EnrollmentService();

        var result = await Run(userManager, enrollment, Config("admin"));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TWO_FACTOR_REQUIRED");
    }

    /// <summary>
    /// Con el servicio de enrolamiento de verdad: desactivar tiene que apagar el 2FA, borrar la
    /// marca de enrolamiento y <b>reiniciar la clave del autenticador</b>. Lo tercero es lo que se
    /// olvida — y dejar la clave viva significa que el usuario cree haber desactivado algo que
    /// sigue funcionando en cuanto vuelva a activarlo.
    /// </summary>
    [Fact]
    public async Task Handle_ResetsTheAuthenticatorKey()
    {
        var user        = User();
        var userManager = ManagerFor(user, "Ambassador");

        userManager.Setup(m => m.SetTwoFactorEnabledAsync(user, false))
                   .Callback(() => user.TwoFactorEnabled = false)
                   .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.ResetAuthenticatorKeyAsync(user))
                   .ReturnsAsync(IdentityResult.Success);

        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(FixedNow);
        clock.Setup(c => c.UtcNow).Returns(FixedNow);

        var enrollment = new TotpEnrollmentService(
            userManager.Object, clock.Object, new ConfigurationBuilder().Build());

        var handler = new DisableTwoFactorHandler(userManager.Object, enrollment, Config("Admin"));

        var result = await handler.Handle(new DisableTwoFactorCommand(UserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);

        userManager.Verify(m => m.ResetAuthenticatorKeyAsync(user), Times.Once);
        userManager.Verify(m => m.SetTwoFactorEnabledAsync(user, false), Times.Once);

        user.TwoFactorEnabled.Should().BeFalse();
        user.TwoFactorEnrolledAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsUserNotFound()
    {
        var result = await Run(ManagerFor(null), EnrollmentService(), Config());

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }
}
