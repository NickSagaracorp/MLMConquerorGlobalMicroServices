using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.Signups.DTOs.Auth;
using MLMConquerorGlobalEdition.Signups.Features.Auth.Commands.Login;
using MLMConquerorGlobalEdition.Signups.Tests.Helpers;

namespace MLMConquerorGlobalEdition.Signups.Tests.Features.Auth;

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
                It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .Returns(accessToken);
        jwt.Setup(j => j.GenerateRefreshToken()).Returns(refreshToken);
        jwt.Setup(j => j.AccessTokenExpiry).Returns(accessExpiry ?? TimeSpan.FromMinutes(60));
        jwt.Setup(j => j.RefreshTokenExpiry).Returns(refreshExpiry ?? TimeSpan.FromDays(30));
        return jwt;
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsInvalidCredentials()
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByEmailAsync("notfound@test.com"))
                   .ReturnsAsync((ApplicationUser?)null);

        var handler = new LoginHandler(userManager.Object, CreateJwtService().Object, DateTimeProvider().Object);

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
        var inactiveUser = new ApplicationUser
        {
            Id = "user-001",
            Email = "inactive@test.com",
            IsActive = false
        };
        userManager.Setup(m => m.FindByEmailAsync("inactive@test.com")).ReturnsAsync(inactiveUser);

        var handler = new LoginHandler(userManager.Object, CreateJwtService().Object, DateTimeProvider().Object);

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

        var handler = new LoginHandler(userManager.Object, CreateJwtService().Object, DateTimeProvider().Object);

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
            Id = "user-001",
            Email = "amb@test.com",
            IsActive = true,
            MemberProfileId = "AMB-000001"
        };
        userManager.Setup(m => m.FindByEmailAsync("amb@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "correct-pass")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user))
                   .ReturnsAsync(new List<string> { "Ambassador" });
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var jwt = CreateJwtService("access-tok", "refresh-tok");
        var handler = new LoginHandler(userManager.Object, jwt.Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "amb@test.com", Password = "correct-pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MemberType.Should().Be("Ambassador");
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
            Id = "user-002",
            Email = "member@test.com",
            IsActive = true,
            MemberProfileId = "MBR-000001"
        };
        userManager.Setup(m => m.FindByEmailAsync("member@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "pass")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user))
                   .ReturnsAsync(new List<string> { "Member" });
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var handler = new LoginHandler(userManager.Object, CreateJwtService().Object, DateTimeProvider().Object);

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
        var user = new ApplicationUser
        {
            Id = "user-003",
            Email = "admin@test.com",
            IsActive = true,
            MemberProfileId = null
        };
        userManager.Setup(m => m.FindByEmailAsync("admin@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "pass")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user))
                   .ReturnsAsync(new List<string> { "Admin" });
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var handler = new LoginHandler(userManager.Object, CreateJwtService().Object, DateTimeProvider().Object);

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
        var user = new ApplicationUser
        {
            Id = "user-001",
            Email = "amb@test.com",
            IsActive = true,
            MemberProfileId = "AMB-000001"
        };
        userManager.Setup(m => m.FindByEmailAsync("amb@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "pass")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user))
                   .ReturnsAsync(new List<string> { "Ambassador" });
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var jwt = CreateJwtService(refreshToken: "raw-refresh-token");
        var handler = new LoginHandler(userManager.Object, jwt.Object, DateTimeProvider().Object);

        await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "amb@test.com", Password = "pass" }),
            CancellationToken.None);

        // RefreshToken stored on user should be SHA256 hash of "raw-refresh-token", not the raw value
        user.RefreshToken.Should().NotBeNullOrEmpty();
        user.RefreshToken.Should().NotBe("raw-refresh-token");
        user.RefreshTokenExpiry.Should().Be(FixedNow.AddDays(30));
        user.LastLoginAt.Should().Be(FixedNow);
    }
}
