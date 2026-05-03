using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.VerifyTwoFactor;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

public class VerifyTwoFactorHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    private const string ValidCode    = "654321";
    private const string ValidCodeHash = "valid-hash";

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

    private static Mock<ITwoFactorChallengeService> CreateChallengeService(
        Result<TwoFactorChallengeClaims>? validationResult = null)
    {
        var m = new Mock<ITwoFactorChallengeService>();

        // Default: succeed for "valid-jwt" carrying ValidCodeHash for user-2fa
        var defaultClaims = new TwoFactorChallengeClaims(
            "user-2fa", "tfa@test.com", ValidCodeHash, FixedNow, FixedNow.AddMinutes(5));
        m.Setup(s => s.ValidateChallenge("valid-jwt", false))
            .Returns(validationResult ?? Result<TwoFactorChallengeClaims>.Success(defaultClaims));

        // Hash returns the input as-is mapped: only the valid code maps to valid hash
        m.Setup(s => s.HashCode(ValidCode)).Returns(ValidCodeHash);
        m.Setup(s => s.HashCode(It.Is<string>(c => c != ValidCode))).Returns("other-hash");
        return m;
    }

    private static VerifyTwoFactorHandler BuildHandler(
        Mock<UserManager<ApplicationUser>> userManager,
        AppDbContext? db = null,
        Mock<IJwtService>? jwt = null,
        Mock<IDateTimeProvider>? dateTime = null,
        Mock<ITwoFactorChallengeService>? twoFactor = null)
        => new(
            userManager.Object,
            (jwt       ?? CreateJwtService()).Object,
            (dateTime  ?? DateTimeProvider()).Object,
            db        ?? InMemoryDbHelper.Create(),
            (twoFactor ?? CreateChallengeService()).Object);

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12345a")]
    public async Task Handle_WhenCodeIsMalformed_ReturnsInvalidCode(string code)
    {
        var userManager = UserManagerHelper.Create();
        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new VerifyTwoFactorCommand(new VerifyTwoFactorRequest { ChallengeToken = "valid-jwt", Code = code }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CODE");
    }

    [Fact]
    public async Task Handle_WhenChallengeExpired_ReturnsCodeExpired()
    {
        var userManager = UserManagerHelper.Create();
        var twoFactor = CreateChallengeService(
            Result<TwoFactorChallengeClaims>.Failure("CODE_EXPIRED", "expired"));
        var handler = BuildHandler(userManager, twoFactor: twoFactor);

        var result = await handler.Handle(
            new VerifyTwoFactorCommand(new VerifyTwoFactorRequest { ChallengeToken = "valid-jwt", Code = ValidCode }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CODE_EXPIRED");
    }

    [Fact]
    public async Task Handle_WhenChallengeSignatureInvalid_ReturnsInvalidChallenge()
    {
        var userManager = UserManagerHelper.Create();
        var twoFactor = CreateChallengeService(
            Result<TwoFactorChallengeClaims>.Failure("INVALID_CHALLENGE", "bad sig"));
        var handler = BuildHandler(userManager, twoFactor: twoFactor);

        var result = await handler.Handle(
            new VerifyTwoFactorCommand(new VerifyTwoFactorRequest { ChallengeToken = "valid-jwt", Code = ValidCode }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CHALLENGE");
    }

    [Fact]
    public async Task Handle_WhenCodeDoesNotMatch_ReturnsInvalidCode()
    {
        var userManager = UserManagerHelper.Create();
        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new VerifyTwoFactorCommand(new VerifyTwoFactorRequest { ChallengeToken = "valid-jwt", Code = "999999" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CODE");
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
        var userManager = UserManagerHelper.Create();
        var user = new ApplicationUser
        {
            Id              = "user-2fa",
            Email           = "tfa@test.com",
            IsActive        = true,
            MemberProfileId = "AMB-000007"
        };
        userManager.Setup(m => m.FindByIdAsync("user-2fa")).ReturnsAsync(user);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Ambassador" });
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

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

        // Refresh token stored hashed (not the raw value).
        user.RefreshToken.Should().NotBeNullOrEmpty();
        user.RefreshToken.Should().NotBe("issued-refresh-token");
        user.RefreshTokenExpiry.Should().Be(FixedNow.AddDays(30));
        user.LastLoginAt.Should().Be(FixedNow);
    }
}
