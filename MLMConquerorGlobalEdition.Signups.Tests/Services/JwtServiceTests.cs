using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.SignupAPI.Services;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Services;

public class JwtServiceTests
{
    private static string GeneratePrivateKeyBase64()
    {
        using var rsa = RSA.Create(2048);
        return Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
    }

    private static IConfiguration BuildConfig(
        int accessExpiryMinutes = 60,
        int refreshExpiryDays = 30)
    {
        var data = new Dictionary<string, string?>
        {
            ["Jwt:PrivateKeyBase64"]          = GeneratePrivateKeyBase64(),
            ["Jwt:Issuer"]                    = "MLMConqueror",
            ["Jwt:Audience"]                  = "MLMConquerorUsers",
            ["Jwt:AccessTokenExpiryMinutes"]  = accessExpiryMinutes.ToString(),
            ["Jwt:RefreshTokenExpiryDays"]    = refreshExpiryDays.ToString()
        };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
    }

    [Fact]
    public void Constructor_WhenJwtKeyMissing_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"]   = "MLMConqueror",
                ["Jwt:Audience"] = "MLMConquerorUsers"
                // Jwt:PrivateKeyBase64 intentionally omitted
            })
            .Build();

        Action act = () => _ = new JwtService(config);

        act.Should().Throw<InvalidOperationException>().WithMessage("*PrivateKeyBase64*");
    }

    [Fact]
    public void AccessTokenExpiry_ReturnsConfiguredValue()
    {
        var service = new JwtService(BuildConfig(accessExpiryMinutes: 30));
        service.AccessTokenExpiry.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void RefreshTokenExpiry_ReturnsConfiguredValue()
    {
        var service = new JwtService(BuildConfig(refreshExpiryDays: 7));
        service.RefreshTokenExpiry.Should().Be(TimeSpan.FromDays(7));
    }

    [Fact]
    public void GenerateAccessToken_ReturnsValidJwtWithExpectedClaims()
    {
        var service = new JwtService(BuildConfig());
        var token = service.GenerateAccessToken(
            userId: "user-001",
            memberId: "AMB-000001",
            email: "test@test.com",
            roles: new[] { "Ambassador" });

        token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Subject.Should().Be("user-001");
        jwt.Claims.First(c => c.Type == "memberId").Value.Should().Be("AMB-000001");
        jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value.Should().Be("test@test.com");
        jwt.Claims.First(c => c.Type == "impersonating").Value.Should().Be("false");
        jwt.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "Ambassador").Should().BeTrue();
    }

    [Fact]
    public void GenerateAccessToken_WhenImpersonating_IncludesImpersonatingClaims()
    {
        var service = new JwtService(BuildConfig());
        var token = service.GenerateAccessToken(
            userId: "admin-001",
            memberId: "AMB-000001",
            email: "admin@test.com",
            roles: new[] { "Admin" },
            isImpersonating: true,
            impersonatedBy: "admin-001");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.First(c => c.Type == "impersonating").Value.Should().Be("true");
        jwt.Claims.Any(c => c.Type == "impersonatedBy" && c.Value == "admin-001").Should().BeTrue();
    }

    [Fact]
    public void GenerateAccessToken_WithMultipleRoles_IncludesAllRoleClaims()
    {
        var service = new JwtService(BuildConfig());
        var token = service.GenerateAccessToken(
            userId: "user-001",
            memberId: "AMB-000001",
            email: "test@test.com",
            roles: new[] { "Ambassador", "Manager" });

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value)
            .Should().Contain("Ambassador").And.Contain("Manager");
    }

    [Fact]
    public void GenerateAccessToken_TokenExpiresAccordingToConfig()
    {
        var service = new JwtService(BuildConfig(accessExpiryMinutes: 15));
        var before = DateTime.UtcNow;
        var token = service.GenerateAccessToken("u", "m", "e@e.com", Array.Empty<string>());
        var after = DateTime.UtcNow;

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.ValidTo.Should().BeCloseTo(before.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsNonEmptyBase64String()
    {
        var service = new JwtService(BuildConfig());
        var token = service.GenerateRefreshToken();

        token.Should().NotBeNullOrEmpty();
        // Should be valid base64 (88 chars for 64 bytes)
        Action act = () => Convert.FromBase64String(token);
        act.Should().NotThrow();
        Convert.FromBase64String(token).Length.Should().Be(64);
    }

    [Fact]
    public void GenerateRefreshToken_EachCallProducesDifferentToken()
    {
        var service = new JwtService(BuildConfig());
        var token1 = service.GenerateRefreshToken();
        var token2 = service.GenerateRefreshToken();

        token1.Should().NotBe(token2);
    }
}
