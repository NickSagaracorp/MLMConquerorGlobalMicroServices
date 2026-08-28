using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Enrollment;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

public class BeginEnrollmentHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    private const string EnrollmentJwt = "enrollment-jwt";
    private const string LoginJwt      = "login-jwt";

    private static ChallengeClaims EnrollmentClaims() => new(
        Jti:          "jti-enroll",
        UserId:       "user-adm",
        Email:        "admin@test.com",
        Purpose:      TwoFactorPurpose.Enrollment,
        OperationKey: null,
        Channel:      TwoFactorChannel.Authenticator,
        CodeHash:     null,
        IssuedAt:     FixedNow,
        ExpiresAt:    FixedNow.AddMinutes(5));

    /// <summary>
    /// Doble del challenge firmado. Solo el token de propósito Enrollment valida: el de login
    /// se rechaza igual que lo haría el servicio de verdad, que compara el claim <c>purpose</c>
    /// con el propósito que espera el endpoint.
    /// </summary>
    private static Mock<IChallengeTokenService> CreateChallengeService()
    {
        var m = new Mock<IChallengeTokenService>();

        m.Setup(s => s.Validate(It.IsAny<string>(), It.IsAny<TwoFactorPurpose>(), It.IsAny<string?>(), It.IsAny<bool>()))
         .Returns(Result<ChallengeClaims>.Failure("INVALID_CHALLENGE", "Challenge token is invalid."));

        m.Setup(s => s.Validate(EnrollmentJwt, TwoFactorPurpose.Enrollment, null, false))
         .Returns(Result<ChallengeClaims>.Success(EnrollmentClaims()));

        return m;
    }

    private static Mock<ITotpEnrollmentService> CreateEnrollmentService(
        Result<TotpEnrollment>? beginResult = null)
    {
        var m = new Mock<ITotpEnrollmentService>();
        m.Setup(s => s.BeginAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(beginResult ?? Result<TotpEnrollment>.Success(new TotpEnrollment(
             "JBSWY3DPEHPK3PXP",
             "otpauth://totp/MLMConqueror:admin@test.com?secret=JBSWY3DPEHPK3PXP&issuer=MLMConqueror&digits=6&period=30",
             "data:image/png;base64,AAAA")));
        return m;
    }

    private static Mock<UserManager<ApplicationUser>> UserManagerWithUser(bool isActive = true)
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync("user-adm"))
                   .ReturnsAsync(new ApplicationUser
                   {
                       Id       = "user-adm",
                       Email    = "admin@test.com",
                       IsActive = isActive
                   });
        return userManager;
    }

    private static BeginEnrollmentHandler BuildHandler(
        Mock<UserManager<ApplicationUser>> userManager,
        Mock<IChallengeTokenService>? challenges = null,
        Mock<ITotpEnrollmentService>? enrollment = null)
        => new(
            userManager.Object,
            (challenges ?? CreateChallengeService()).Object,
            (enrollment ?? CreateEnrollmentService()).Object);

    [Fact]
    public async Task Handle_WhenTokenIsValid_ReturnsSharedKeyUriAndQr()
    {
        var handler = BuildHandler(UserManagerWithUser());

        var result = await handler.Handle(
            new BeginEnrollmentCommand(new BeginEnrollmentRequest { EnrollmentToken = EnrollmentJwt }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SharedKey.Should().Be("JBSWY3DPEHPK3PXP");
        result.Value.AuthenticatorUri.Should().StartWith("otpauth://totp/");
        result.Value.QrCodePngDataUri.Should().StartWith("data:image/png;base64,");
    }

    /// <summary>
    /// Un token de login no sirve para enrolarse. Sin esta separación, el código que abre una
    /// sesión abriría también la configuración del segundo factor — justo lo que ese factor
    /// tiene que proteger.
    /// </summary>
    [Fact]
    public async Task Handle_WhenTokenHasWrongPurpose_ReturnsInvalidChallenge()
    {
        var enrollment = CreateEnrollmentService();
        var handler    = BuildHandler(UserManagerWithUser(), enrollment: enrollment);

        var result = await handler.Handle(
            new BeginEnrollmentCommand(new BeginEnrollmentRequest { EnrollmentToken = LoginJwt }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CHALLENGE");

        // Y no se llega a tocar la clave del autenticador del usuario.
        enrollment.Verify(s => s.BeginAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()),
                          Times.Never);
    }

    [Fact]
    public async Task Handle_ValidatesTheTokenWithEnrollmentPurpose()
    {
        var challenges = CreateChallengeService();
        var handler    = BuildHandler(UserManagerWithUser(), challenges: challenges);

        await handler.Handle(
            new BeginEnrollmentCommand(new BeginEnrollmentRequest { EnrollmentToken = EnrollmentJwt }),
            CancellationToken.None);

        challenges.Verify(s => s.Validate(EnrollmentJwt, TwoFactorPurpose.Enrollment, null, false), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserNoLongerActive_ReturnsInvalidCredentials()
    {
        var handler = BuildHandler(UserManagerWithUser(isActive: false));

        var result = await handler.Handle(
            new BeginEnrollmentCommand(new BeginEnrollmentRequest { EnrollmentToken = EnrollmentJwt }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_WhenBeginFails_PropagatesThatError()
    {
        var enrollment = CreateEnrollmentService(
            Result<TotpEnrollment>.Failure("ENROLLMENT_FAILED", "No se pudo generar la clave del autenticador."));
        var handler = BuildHandler(UserManagerWithUser(), enrollment: enrollment);

        var result = await handler.Handle(
            new BeginEnrollmentCommand(new BeginEnrollmentRequest { EnrollmentToken = EnrollmentJwt }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ENROLLMENT_FAILED");
    }
}
