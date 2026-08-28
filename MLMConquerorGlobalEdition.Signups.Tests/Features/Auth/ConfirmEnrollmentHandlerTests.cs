using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Enrollment;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

public class ConfirmEnrollmentHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    private const string EnrollmentJwt = "enrollment-jwt";
    private const string LoginJwt      = "login-jwt";
    private const string ValidCode     = "123456";

    private static Mock<IDateTimeProvider> DateTimeProvider()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static Mock<IJwtService> CreateJwtService()
    {
        var jwt = new Mock<IJwtService>();
        jwt.Setup(j => j.GenerateAccessToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns("issued-access-token");
        jwt.Setup(j => j.GenerateRefreshToken()).Returns("issued-refresh-token");
        jwt.Setup(j => j.AccessTokenExpiry).Returns(TimeSpan.FromMinutes(15));
        jwt.Setup(j => j.RefreshTokenExpiry).Returns(TimeSpan.FromDays(30));
        return jwt;
    }

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

    private static Mock<IChallengeTokenService> CreateChallengeService()
    {
        var m = new Mock<IChallengeTokenService>();

        m.Setup(s => s.Validate(It.IsAny<string>(), It.IsAny<TwoFactorPurpose>(), It.IsAny<string?>(), It.IsAny<bool>()))
         .Returns(Result<ChallengeClaims>.Failure("INVALID_CHALLENGE", "Challenge token is invalid."));

        m.Setup(s => s.Validate(EnrollmentJwt, TwoFactorPurpose.Enrollment, null, false))
         .Returns(Result<ChallengeClaims>.Success(EnrollmentClaims()));

        return m;
    }

    /// <summary>
    /// Doble del enrolamiento TOTP: solo <see cref="ValidCode"/> confirma. La librería de
    /// verdad activa el 2FA únicamente en ese caso, y no toca nada cuando el código falla.
    /// </summary>
    private static Mock<ITotpEnrollmentService> CreateEnrollmentService()
    {
        var m = new Mock<ITotpEnrollmentService>();

        m.Setup(s => s.ConfirmAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<bool>.Failure("CODE_INVALID", "El código introducido no es válido."));

        m.Setup(s => s.ConfirmAsync(It.IsAny<ApplicationUser>(), ValidCode, It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<bool>.Success(true));

        return m;
    }

    private static Mock<UserManager<ApplicationUser>> UserManagerWithUser(
        out ApplicationUser user, bool isActive = true)
    {
        user = new ApplicationUser
        {
            Id              = "user-adm",
            Email           = "admin@test.com",
            IsActive        = isActive,
            MemberProfileId = null
        };
        var captured = user;

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync("user-adm")).ReturnsAsync(captured);
        userManager.Setup(m => m.GetRolesAsync(captured)).ReturnsAsync(new List<string> { "Admin" });
        userManager.Setup(m => m.UpdateAsync(captured)).ReturnsAsync(IdentityResult.Success);
        return userManager;
    }

    private static ConfirmEnrollmentHandler BuildHandler(
        Mock<UserManager<ApplicationUser>> userManager,
        AppDbContext? db = null,
        Mock<IJwtService>? jwt = null,
        Mock<IChallengeTokenService>? challenges = null,
        Mock<ITotpEnrollmentService>? enrollment = null)
        => new(
            userManager.Object,
            (jwt ?? CreateJwtService()).Object,
            DateTimeProvider().Object,
            db  ?? InMemoryDbHelper.Create(),
            (challenges ?? CreateChallengeService()).Object,
            (enrollment ?? CreateEnrollmentService()).Object);

    /// <summary>
    /// Confirmado el enrolamiento, el usuario queda dentro: acaba de demostrar los dos factores
    /// en la misma sesión, así que mandarlo de vuelta al login solo añadiría una pantalla.
    /// </summary>
    [Fact]
    public async Task Handle_WhenCodeIsValid_IssuesAccessTokens()
    {
        var userManager = UserManagerWithUser(out var user);
        var handler     = BuildHandler(userManager);

        var result = await handler.Handle(
            new ConfirmEnrollmentCommand(new ConfirmEnrollmentRequest
            {
                EnrollmentToken = EnrollmentJwt, Code = ValidCode
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.UserId.Should().Be("user-adm");
        result.Value.AccessToken.Should().Be("issued-access-token");
        result.Value.RefreshToken.Should().Be("issued-refresh-token");
        result.Value.MemberType.Should().Be("Staff");
        result.Value.TokenExpiry.Should().Be(FixedNow.AddMinutes(15));
        result.Value.RequiresEnrollment.Should().BeFalse();

        // El refresh token se guarda hasheado, igual que en el resto de los caminos.
        user.RefreshToken.Should().NotBeNullOrEmpty();
        user.RefreshToken.Should().NotBe("issued-refresh-token");
        user.RefreshTokenExpiry.Should().Be(FixedNow.AddDays(30));
        user.LastLoginAt.Should().Be(FixedNow);
    }

    /// <summary>Un token de login no sirve para terminar un enrolamiento.</summary>
    [Fact]
    public async Task Handle_WhenTokenHasWrongPurpose_ReturnsInvalidChallengeAndIssuesNothing()
    {
        var userManager = UserManagerWithUser(out _);
        var enrollment  = CreateEnrollmentService();
        var jwt         = CreateJwtService();
        var handler     = BuildHandler(userManager, jwt: jwt, enrollment: enrollment);

        var result = await handler.Handle(
            new ConfirmEnrollmentCommand(new ConfirmEnrollmentRequest
            {
                EnrollmentToken = LoginJwt, Code = ValidCode
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CHALLENGE");

        enrollment.Verify(s => s.ConfirmAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        jwt.Verify(j => j.GenerateRefreshToken(), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCodeIsWrong_ReturnsCodeInvalid()
    {
        var userManager = UserManagerWithUser(out _);
        var handler     = BuildHandler(userManager);

        var result = await handler.Handle(
            new ConfirmEnrollmentCommand(new ConfirmEnrollmentRequest
            {
                EnrollmentToken = EnrollmentJwt, Code = "999999"
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CODE_INVALID");
    }

    /// <summary>
    /// Un enrolamiento que no se confirmó no puede terminar en una sesión abierta: sin tokens,
    /// sin refresh token persistido y sin marca de último acceso.
    /// </summary>
    [Fact]
    public async Task Handle_WhenConfirmFails_DoesNotIssueTokens()
    {
        var userManager = UserManagerWithUser(out var user);
        var jwt         = CreateJwtService();
        var handler     = BuildHandler(userManager, jwt: jwt);

        var result = await handler.Handle(
            new ConfirmEnrollmentCommand(new ConfirmEnrollmentRequest
            {
                EnrollmentToken = EnrollmentJwt, Code = "999999"
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();

        jwt.Verify(j => j.GenerateAccessToken(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);
        jwt.Verify(j => j.GenerateRefreshToken(), Times.Never);
        userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);

        user.RefreshToken.Should().BeNull();
        user.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenUserNoLongerActive_ReturnsInvalidCredentials()
    {
        var userManager = UserManagerWithUser(out _, isActive: false);
        var handler     = BuildHandler(userManager);

        var result = await handler.Handle(
            new ConfirmEnrollmentCommand(new ConfirmEnrollmentRequest
            {
                EnrollmentToken = EnrollmentJwt, Code = ValidCode
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CREDENTIALS");
    }
}
