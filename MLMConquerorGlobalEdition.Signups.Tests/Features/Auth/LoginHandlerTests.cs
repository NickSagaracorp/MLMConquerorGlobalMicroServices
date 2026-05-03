using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Login;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

public class LoginHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> DateTimeProvider()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static Mock<IJwtService> CreateJwtService(
        string accessToken = "access-token",
        string refreshToken = "refresh-token",
        TimeSpan? accessExpiry = null,
        TimeSpan? refreshExpiry = null)
    {
        var jwt = new Mock<IJwtService>();
        jwt.Setup(j => j.GenerateAccessToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(accessToken);
        jwt.Setup(j => j.GenerateRefreshToken()).Returns(refreshToken);
        jwt.Setup(j => j.AccessTokenExpiry).Returns(accessExpiry ?? TimeSpan.FromMinutes(60));
        jwt.Setup(j => j.RefreshTokenExpiry).Returns(refreshExpiry ?? TimeSpan.FromDays(30));
        return jwt;
    }

    private static Mock<ITwoFactorChallengeService> CreateChallengeService(
        string code = "123456",
        string codeHash = "hash",
        string challenge = "challenge-jwt",
        string maskedEmail = "u***@test.com")
    {
        var m = new Mock<ITwoFactorChallengeService>();
        m.Setup(s => s.GenerateCode()).Returns(code);
        m.Setup(s => s.HashCode(It.IsAny<string>())).Returns(codeHash);
        m.Setup(s => s.IssueChallenge(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(challenge);
        m.Setup(s => s.MaskEmail(It.IsAny<string>())).Returns(maskedEmail);
        m.Setup(s => s.ChallengeLifetime).Returns(TimeSpan.FromMinutes(5));
        return m;
    }

    private static Mock<IEmailService> CreateEmailService() => new();

    private static LoginHandler BuildHandler(
        Mock<UserManager<ApplicationUser>> userManager,
        AppDbContext? db = null,
        Mock<IJwtService>? jwt = null,
        Mock<IDateTimeProvider>? dateTime = null,
        Mock<ITwoFactorChallengeService>? twoFactor = null,
        Mock<IEmailService>? email = null)
        => new(
            userManager.Object,
            (jwt       ?? CreateJwtService()).Object,
            (dateTime  ?? DateTimeProvider()).Object,
            db        ?? InMemoryDbHelper.Create(),
            (twoFactor ?? CreateChallengeService()).Object,
            (email     ?? CreateEmailService()).Object);

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsInvalidCredentials()
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByEmailAsync("notfound@test.com"))
                   .ReturnsAsync((ApplicationUser?)null);

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "notfound@test.com", Password = "pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_WhenUserIsInactive_ReturnsInvalidCredentials()
    {
        var userManager = UserManagerHelper.Create();
        var inactiveUser = new ApplicationUser { Id = "user-001", Email = "inactive@test.com", IsActive = false };
        userManager.Setup(m => m.FindByEmailAsync("inactive@test.com")).ReturnsAsync(inactiveUser);

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "inactive@test.com", Password = "pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_WhenPasswordIsInvalid_ReturnsInvalidCredentials()
    {
        var userManager = UserManagerHelper.Create();
        var user = new ApplicationUser { Id = "user-001", Email = "user@test.com", IsActive = true };
        userManager.Setup(m => m.FindByEmailAsync("user@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "wrong-password")).ReturnsAsync(false);

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "user@test.com", Password = "wrong-password" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_WhenAmbassadorLogin_ReturnsMemberTypeAmbassador()
    {
        var userManager = UserManagerHelper.Create();
        var user = new ApplicationUser
        {
            Id = "user-001", Email = "amb@test.com", IsActive = true, MemberProfileId = "AMB-000001"
        };
        userManager.Setup(m => m.FindByEmailAsync("amb@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "correct-pass")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Ambassador" });
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var jwt = CreateJwtService("access-tok", "refresh-tok");
        var handler = BuildHandler(userManager, jwt: jwt);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "amb@test.com", Password = "correct-pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresTwoFactor.Should().BeFalse();
        result.Value.MemberType.Should().Be("Ambassador");
        result.Value.MemberId.Should().Be("AMB-000001");
        result.Value.AccessToken.Should().Be("access-tok");
        result.Value.RefreshToken.Should().Be("refresh-tok");
        result.Value.TokenExpiry.Should().Be(FixedNow.AddMinutes(60));
    }

    [Fact]
    public async Task Handle_WhenMemberLogin_ReturnsMemberTypeMember()
    {
        var userManager = UserManagerHelper.Create();
        var user = new ApplicationUser
        {
            Id = "user-002", Email = "member@test.com", IsActive = true, MemberProfileId = "MBR-000001"
        };
        userManager.Setup(m => m.FindByEmailAsync("member@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "pass")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Member" });
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "member@test.com", Password = "pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MemberType.Should().Be("Member");
    }

    [Fact]
    public async Task Handle_WhenAdminLogin_ReturnsMemberTypeStaff()
    {
        var userManager = UserManagerHelper.Create();
        var user = new ApplicationUser { Id = "user-003", Email = "admin@test.com", IsActive = true, MemberProfileId = null };
        userManager.Setup(m => m.FindByEmailAsync("admin@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "pass")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "admin@test.com", Password = "pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MemberType.Should().Be("Staff");
    }

    [Fact]
    public async Task Handle_WhenLoginSucceeds_StoresHashedRefreshTokenOnUser()
    {
        var userManager = UserManagerHelper.Create();
        var user = new ApplicationUser { Id = "user-001", Email = "amb@test.com", IsActive = true, MemberProfileId = "AMB-000001" };
        userManager.Setup(m => m.FindByEmailAsync("amb@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "pass")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Ambassador" });
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var jwt = CreateJwtService(refreshToken: "raw-refresh-token");
        var handler = BuildHandler(userManager, jwt: jwt);

        await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "amb@test.com", Password = "pass" }),
            CancellationToken.None);

        user.RefreshToken.Should().NotBeNullOrEmpty();
        user.RefreshToken.Should().NotBe("raw-refresh-token");
        user.RefreshTokenExpiry.Should().Be(FixedNow.AddDays(30));
        user.LastLoginAt.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Handle_WhenTwoFactorEnabled_ReturnsChallengeAndDoesNotIssueTokens()
    {
        var userManager = UserManagerHelper.Create();
        var user = new ApplicationUser
        {
            Id                = "user-2fa",
            Email             = "tfa@test.com",
            IsActive          = true,
            TwoFactorEnabled  = true,
            MemberProfileId   = null
        };
        userManager.Setup(m => m.FindByEmailAsync("tfa@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "pass")).ReturnsAsync(true);

        var twoFactor = CreateChallengeService(challenge: "issued-jwt", maskedEmail: "t***@test.com");
        var email     = CreateEmailService();
        var handler   = BuildHandler(userManager, twoFactor: twoFactor, email: email);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "tfa@test.com", Password = "pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresTwoFactor.Should().BeTrue();
        result.Value.ChallengeToken.Should().Be("issued-jwt");
        result.Value.MaskedEmail.Should().Be("t***@test.com");
        result.Value.AccessToken.Should().BeEmpty();
        result.Value.RefreshToken.Should().BeEmpty();

        // No refresh-token persistence yet — that happens after successful verify.
        user.RefreshToken.Should().BeNull();
        user.LastLoginAt.Should().BeNull();
        userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);

        // Code email was dispatched with the canonical event type.
        email.Verify(e => e.SendAsync(
            "tfa@test.com", "tfa@test.com", "en",
            NotificationEvents.TwoFactorCode,
            It.Is<Dictionary<string, string>>(d => d.ContainsKey("Code") && d["Code"] == "123456"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
