using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.VerifyTwoFactor;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

public class VerifyTwoFactorHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    private const string ValidCode = "654321";

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

    private static ChallengeClaims LoginClaims() => new(
        Jti:          "jti-1",
        UserId:       "user-2fa",
        Email:        "tfa@test.com",
        Purpose:      TwoFactorPurpose.Login,
        OperationKey: null,
        Channel:      TwoFactorChannel.Email,
        CodeHash:     "valid-hash",
        IssuedAt:     FixedNow,
        ExpiresAt:    FixedNow.AddMinutes(5));

    /// <summary>
    /// El doble de la librería <c>Authn</c>. El handler ya no hashea el código ni lo compara: le
    /// pasa el challenge y el código a <c>VerifyAsync</c> y devuelve lo que le den. Por defecto
    /// solo <see cref="ValidCode"/> verifica; cualquier otro código da <c>CODE_INVALID</c>, que
    /// es lo que devuelve la librería de verdad.
    /// </summary>
    private static Mock<ITwoFactorService> CreateTwoFactorService(
        Result<ChallengeClaims>? verifyResult = null)
    {
        var m = new Mock<ITwoFactorService>();

        m.Setup(s => s.VerifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TwoFactorPurpose>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(verifyResult ?? Result<ChallengeClaims>.Failure(
                "CODE_INVALID", "El código introducido no es válido."));

        if (verifyResult is null)
            m.Setup(s => s.VerifyAsync(
                    It.IsAny<string>(), ValidCode, It.IsAny<TwoFactorPurpose>(),
                    It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ChallengeClaims>.Success(LoginClaims()));

        return m;
    }

    private static VerifyTwoFactorHandler BuildHandler(
        Mock<UserManager<ApplicationUser>> userManager,
        AppDbContext? db = null,
        Mock<IJwtService>? jwt = null,
        Mock<IDateTimeProvider>? dateTime = null,
        Mock<ITwoFactorService>? twoFactor = null)
        => new(
            userManager.Object,
            (jwt       ?? CreateJwtService()).Object,
            (dateTime  ?? DateTimeProvider()).Object,
            db        ?? InMemoryDbHelper.Create(),
            (twoFactor ?? CreateTwoFactorService()).Object);

    private static Mock<UserManager<ApplicationUser>> UserManagerWithActiveUser(out ApplicationUser user)
    {
        user = new ApplicationUser
        {
            Id              = "user-2fa",
            Email           = "tfa@test.com",
            IsActive        = true,
            MemberProfileId = "AMB-000007"
        };
        var captured = user;

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync("user-2fa")).ReturnsAsync(captured);
        userManager.Setup(m => m.GetRolesAsync(captured)).ReturnsAsync(new List<string> { "Ambassador" });
        userManager.Setup(m => m.UpdateAsync(captured)).ReturnsAsync(IdentityResult.Success);
        return userManager;
    }

    /// <summary>
    /// El punto de toda la migración: el challenge que emite <c>LoginHandler</c> lleva el
    /// propósito <c>Login</c>, así que aquí hay que redimirlo con ese mismo propósito. Con
    /// cualquier otro, la librería rechazaría un código recién emitido por el inicio de sesión.
    /// </summary>
    [Fact]
    public async Task Handle_VerifiesTheChallengeWithLoginPurpose()
    {
        var userManager = UserManagerWithActiveUser(out _);
        var twoFactor   = CreateTwoFactorService();
        var handler     = BuildHandler(userManager, twoFactor: twoFactor);

        await handler.Handle(
            new VerifyTwoFactorCommand(new VerifyTwoFactorRequest { ChallengeToken = "valid-jwt", Code = ValidCode }),
            CancellationToken.None);

        twoFactor.Verify(s => s.VerifyAsync(
            "valid-jwt", ValidCode, TwoFactorPurpose.Login, null, It.IsAny<CancellationToken>()),
            Times.Once);
        twoFactor.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12345a")]
    public async Task Handle_WhenCodeIsMalformed_ReturnsCodeInvalid(string code)
    {
        var userManager = UserManagerHelper.Create();
        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new VerifyTwoFactorCommand(new VerifyTwoFactorRequest { ChallengeToken = "valid-jwt", Code = code }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CODE_INVALID");
    }

    [Fact]
    public async Task Handle_WhenChallengeExpired_ReturnsCodeExpired()
    {
        var userManager = UserManagerHelper.Create();
        var twoFactor = CreateTwoFactorService(
            Result<ChallengeClaims>.Failure("CODE_EXPIRED", "The verification code has expired."));
        var handler = BuildHandler(userManager, twoFactor: twoFactor);

        var result = await handler.Handle(
            new VerifyTwoFactorCommand(new VerifyTwoFactorRequest { ChallengeToken = "valid-jwt", Code = ValidCode }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CODE_EXPIRED");
        result.Error.Should().Be("The verification code has expired.");
    }

    [Fact]
    public async Task Handle_WhenChallengeSignatureInvalid_ReturnsInvalidChallenge()
    {
        var userManager = UserManagerHelper.Create();
        var twoFactor = CreateTwoFactorService(
            Result<ChallengeClaims>.Failure("INVALID_CHALLENGE", "Challenge token is invalid."));
        var handler = BuildHandler(userManager, twoFactor: twoFactor);

        var result = await handler.Handle(
            new VerifyTwoFactorCommand(new VerifyTwoFactorRequest { ChallengeToken = "valid-jwt", Code = ValidCode }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CHALLENGE");
    }

    /// <summary>
    /// "Demasiados intentos" no es "código incorrecto": el primero se arregla pidiendo un código
    /// nuevo y el segundo escribiéndolo mejor. Si el handler los colapsara, la interfaz no
    /// podría decirle al usuario cuál de las dos cosas le pasa.
    /// </summary>
    [Fact]
    public async Task Handle_WhenAttemptsExhausted_PropagatesTooManyAttempts()
    {
        var userManager = UserManagerHelper.Create();
        var twoFactor = CreateTwoFactorService(
            Result<ChallengeClaims>.Failure(
                "TOO_MANY_ATTEMPTS", "Demasiados intentos fallidos; solicite un código nuevo."));
        var handler = BuildHandler(userManager, twoFactor: twoFactor);

        var result = await handler.Handle(
            new VerifyTwoFactorCommand(new VerifyTwoFactorRequest { ChallengeToken = "valid-jwt", Code = ValidCode }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOO_MANY_ATTEMPTS");
        result.Error.Should().Be("Demasiados intentos fallidos; solicite un código nuevo.");
    }

    [Fact]
    public async Task Handle_WhenCodeDoesNotMatch_ReturnsCodeInvalid()
    {
        var userManager = UserManagerHelper.Create();
        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new VerifyTwoFactorCommand(new VerifyTwoFactorRequest { ChallengeToken = "valid-jwt", Code = "999999" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CODE_INVALID");
    }

    [Fact]
    public async Task Handle_WhenVerificationFails_DoesNotIssueTokens()
    {
        var userManager = UserManagerWithActiveUser(out var user);
        var jwt         = CreateJwtService();
        var handler     = BuildHandler(userManager, jwt: jwt);

        var result = await handler.Handle(
            new VerifyTwoFactorCommand(new VerifyTwoFactorRequest { ChallengeToken = "valid-jwt", Code = "999999" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        jwt.Verify(j => j.GenerateRefreshToken(), Times.Never);
        userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        user.RefreshToken.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenUserNoLongerActive_ReturnsInvalidCredentials()
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync("user-2fa"))
                   .ReturnsAsync(new ApplicationUser { Id = "user-2fa", IsActive = false, Email = "tfa@test.com" });

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new VerifyTwoFactorCommand(new VerifyTwoFactorRequest { ChallengeToken = "valid-jwt", Code = ValidCode }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_WhenCodeMatches_IssuesTokensAndPersistsRefreshToken()
    {
        var userManager = UserManagerWithActiveUser(out var user);
        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new VerifyTwoFactorCommand(new VerifyTwoFactorRequest { ChallengeToken = "valid-jwt", Code = ValidCode }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresTwoFactor.Should().BeFalse();
        result.Value.AccessToken.Should().Be("issued-access-token");
        result.Value.RefreshToken.Should().Be("issued-refresh-token");
        result.Value.MemberType.Should().Be("Ambassador");
        result.Value.MemberId.Should().Be("AMB-000007");
        result.Value.TokenExpiry.Should().Be(FixedNow.AddMinutes(15));

        // El UserId sale de los claims del challenge, no del cuerpo de la petición.
        result.Value.UserId.Should().Be("user-2fa");

        // Refresh token stored hashed (not the raw value).
        user.RefreshToken.Should().NotBeNullOrEmpty();
        user.RefreshToken.Should().NotBe("issued-refresh-token");
        user.RefreshTokenExpiry.Should().Be(FixedNow.AddDays(30));
        user.LastLoginAt.Should().Be(FixedNow);
    }
}
