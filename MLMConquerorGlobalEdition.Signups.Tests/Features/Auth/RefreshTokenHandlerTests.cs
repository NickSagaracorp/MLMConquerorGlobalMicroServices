using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.RefreshToken;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;
using System.Security.Cryptography;
using System.Text;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

public class RefreshTokenHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static string HashToken(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToBase64String(bytes);
    }

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
                It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .Returns("new-access-token");
        jwt.Setup(j => j.GenerateRefreshToken()).Returns("new-refresh-token");
        jwt.Setup(j => j.AccessTokenExpiry).Returns(TimeSpan.FromMinutes(60));
        jwt.Setup(j => j.RefreshTokenExpiry).Returns(TimeSpan.FromDays(30));
        return jwt;
    }

    [Fact]
    public async Task Handle_WhenTokenNotFound_ReturnsInvalidRefreshToken()
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.Users).Returns(
            new List<ApplicationUser>().AsQueryable());

        var handler = new RefreshTokenHandler(userManager.Object, CreateJwtService().Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new RefreshTokenCommand("unknown-token"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_REFRESH_TOKEN");
    }

    [Fact]
    public async Task Handle_WhenUserIsInactive_ReturnsInvalidRefreshToken()
    {
        var rawToken = "valid-raw-token";
        var hashedToken = HashToken(rawToken);

        var inactiveUser = new ApplicationUser
        {
            Id = "user-001",
            Email = "user@test.com",
            IsActive = false,
            RefreshToken = hashedToken,
            RefreshTokenExpiry = FixedNow.AddDays(10)
        };

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.Users).Returns(
            new List<ApplicationUser> { inactiveUser }.AsQueryable());

        var handler = new RefreshTokenHandler(userManager.Object, CreateJwtService().Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new RefreshTokenCommand(rawToken), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_REFRESH_TOKEN");
    }

    [Fact]
    public async Task Handle_WhenRefreshTokenExpired_ReturnsRefreshTokenExpired()
    {
        var rawToken = "expired-token";
        var hashedToken = HashToken(rawToken);

        var user = new ApplicationUser
        {
            Id = "user-001",
            Email = "user@test.com",
            IsActive = true,
            RefreshToken = hashedToken,
            RefreshTokenExpiry = FixedNow.AddDays(-1) // expired yesterday
        };

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.Users).Returns(
            new List<ApplicationUser> { user }.AsQueryable());

        var handler = new RefreshTokenHandler(userManager.Object, CreateJwtService().Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new RefreshTokenCommand(rawToken), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("REFRESH_TOKEN_EXPIRED");
    }

    [Fact]
    public async Task Handle_WhenValidToken_ReturnsNewAccessAndRefreshTokens()
    {
        var rawToken = "valid-token";
        var hashedToken = HashToken(rawToken);

        var user = new ApplicationUser
        {
            Id = "user-001",
            Email = "amb@test.com",
            IsActive = true,
            MemberProfileId = "AMB-000001",
            RefreshToken = hashedToken,
            RefreshTokenExpiry = FixedNow.AddDays(20) // still valid
        };

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.Users).Returns(
            new List<ApplicationUser> { user }.AsQueryable());
        userManager.Setup(m => m.GetRolesAsync(user))
                   .ReturnsAsync(new List<string> { "Ambassador" });
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var handler = new RefreshTokenHandler(userManager.Object, CreateJwtService().Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new RefreshTokenCommand(rawToken), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().Be("new-access-token");
        result.Value.RefreshToken.Should().Be("new-refresh-token");
        result.Value.MemberId.Should().Be("AMB-000001");
        result.Value.MemberType.Should().Be("Ambassador");
    }

    [Fact]
    public async Task Handle_WhenValidToken_UpdatesStoredRefreshTokenWithNewHash()
    {
        var rawToken = "valid-token";
        var hashedToken = HashToken(rawToken);

        var user = new ApplicationUser
        {
            Id = "user-001",
            Email = "amb@test.com",
            IsActive = true,
            RefreshToken = hashedToken,
            RefreshTokenExpiry = FixedNow.AddDays(20)
        };

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.Users).Returns(
            new List<ApplicationUser> { user }.AsQueryable());
        userManager.Setup(m => m.GetRolesAsync(user))
                   .ReturnsAsync(new List<string> { "Ambassador" });
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var jwt = new Mock<IJwtService>();
        jwt.Setup(j => j.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .Returns("access");
        jwt.Setup(j => j.GenerateRefreshToken()).Returns("new-raw-refresh");
        jwt.Setup(j => j.AccessTokenExpiry).Returns(TimeSpan.FromMinutes(60));
        jwt.Setup(j => j.RefreshTokenExpiry).Returns(TimeSpan.FromDays(30));

        var handler = new RefreshTokenHandler(userManager.Object, jwt.Object, DateTimeProvider().Object);
        await handler.Handle(new RefreshTokenCommand(rawToken), CancellationToken.None);

        var expectedNewHash = HashToken("new-raw-refresh");
        user.RefreshToken.Should().Be(expectedNewHash);
        user.RefreshTokenExpiry.Should().Be(FixedNow.AddDays(30));
    }
}
