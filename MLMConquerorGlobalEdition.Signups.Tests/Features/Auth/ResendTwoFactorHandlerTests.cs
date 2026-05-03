using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ResendTwoFactor;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

public class ResendTwoFactorHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<ITwoFactorChallengeService> CreateChallengeService(
        Result<TwoFactorChallengeClaims>? validationResult = null,
        string newChallenge = "fresh-jwt",
        string newCode = "246810")
    {
        var m = new Mock<ITwoFactorChallengeService>();

        var defaultClaims = new TwoFactorChallengeClaims(
            "user-2fa", "tfa@test.com", "old-hash", FixedNow.AddMinutes(-5), FixedNow);
        m.Setup(s => s.ValidateChallenge(It.IsAny<string>(), true))
            .Returns(validationResult ?? Result<TwoFactorChallengeClaims>.Success(defaultClaims));

        m.Setup(s => s.GenerateCode()).Returns(newCode);
        m.Setup(s => s.HashCode(newCode)).Returns("new-hash");
        m.Setup(s => s.IssueChallenge("user-2fa", "tfa@test.com", "new-hash")).Returns(newChallenge);
        m.Setup(s => s.MaskEmail("tfa@test.com")).Returns("t***@test.com");
        m.Setup(s => s.ChallengeLifetime).Returns(TimeSpan.FromMinutes(5));
        return m;
    }

    private static ResendTwoFactorHandler BuildHandler(
        Mock<UserManager<ApplicationUser>> userManager,
        AppDbContext? db = null,
        Mock<ITwoFactorChallengeService>? twoFactor = null,
        Mock<IEmailService>? email = null)
        => new(
            userManager.Object,
            db        ?? InMemoryDbHelper.Create(),
            (twoFactor ?? CreateChallengeService()).Object,
            (email     ?? new Mock<IEmailService>()).Object);

    [Fact]
    public async Task Handle_WhenChallengeBeyondGraceWindow_ReturnsCodeExpired()
    {
        var userManager = UserManagerHelper.Create();
        var twoFactor = CreateChallengeService(
            Result<TwoFactorChallengeClaims>.Failure("CODE_EXPIRED", "too old"));
        var handler = BuildHandler(userManager, twoFactor: twoFactor);

        var result = await handler.Handle(
            new ResendTwoFactorCommand(new ResendTwoFactorRequest { ChallengeToken = "old-jwt" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CODE_EXPIRED");
    }

    [Fact]
    public async Task Handle_WhenSignatureInvalid_ReturnsInvalidChallenge()
    {
        var userManager = UserManagerHelper.Create();
        var twoFactor = CreateChallengeService(
            Result<TwoFactorChallengeClaims>.Failure("INVALID_CHALLENGE", "bad sig"));
        var handler = BuildHandler(userManager, twoFactor: twoFactor);

        var result = await handler.Handle(
            new ResendTwoFactorCommand(new ResendTwoFactorRequest { ChallengeToken = "tampered" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CHALLENGE");
    }

    [Fact]
    public async Task Handle_WhenUserNoLongerHasTfaEnabled_ReturnsInvalidChallenge()
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync("user-2fa"))
                   .ReturnsAsync(new ApplicationUser
                   {
                       Id               = "user-2fa",
                       Email            = "tfa@test.com",
                       IsActive         = true,
                       TwoFactorEnabled = false
                   });

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new ResendTwoFactorCommand(new ResendTwoFactorRequest { ChallengeToken = "valid-jwt" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CHALLENGE");
    }

    [Fact]
    public async Task Handle_WhenChallengeIsFresh_IssuesNewChallengeAndDispatchesEmail()
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync("user-2fa"))
                   .ReturnsAsync(new ApplicationUser
                   {
                       Id               = "user-2fa",
                       Email            = "tfa@test.com",
                       IsActive         = true,
                       TwoFactorEnabled = true
                   });

        var email = new Mock<IEmailService>();
        var handler = BuildHandler(userManager, email: email);

        var result = await handler.Handle(
            new ResendTwoFactorCommand(new ResendTwoFactorRequest { ChallengeToken = "valid-jwt" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresTwoFactor.Should().BeTrue();
        result.Value.ChallengeToken.Should().Be("fresh-jwt");
        result.Value.MaskedEmail.Should().Be("t***@test.com");
        result.Value.AccessToken.Should().BeEmpty();
        result.Value.RefreshToken.Should().BeEmpty();

        email.Verify(e => e.SendAsync(
            "tfa@test.com", "tfa@test.com", "en",
            NotificationEvents.TwoFactorCode,
            It.Is<Dictionary<string, string>>(d => d["Code"] == "246810" && d["ExpiresInMinutes"] == "5"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
