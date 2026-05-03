using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration  _config;
    private readonly string          _issuer;
    private readonly string          _audience;
    private readonly RsaSecurityKey  _signingKey;

    public JwtService(IConfiguration config)
    {
        _config   = config;
        _issuer   = config["Jwt:Issuer"]   ?? throw new InvalidOperationException("Jwt:Issuer not configured.");
        _audience = config["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience not configured.");

        var privateKeyBase64 = config["Jwt:PrivateKeyBase64"]
            ?? throw new InvalidOperationException("Jwt:PrivateKeyBase64 not configured.");

        var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
        _signingKey = new RsaSecurityKey(rsa);
    }

    public TimeSpan AccessTokenExpiry  => TimeSpan.FromMinutes(_config.GetValue("Jwt:AccessTokenExpiryMinutes", 15));
    public TimeSpan RefreshTokenExpiry => TimeSpan.FromDays(_config.GetValue("Jwt:RefreshTokenExpiryDays", 30));

    public string GenerateAccessToken(
        string userId,
        string memberId,
        string email,
        IEnumerable<string> roles,
        bool isImpersonating = false,
        string? impersonatedBy = null,
        string? defaultLanguage = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new("memberId",      memberId),
            new("impersonating", isImpersonating.ToString().ToLower()),
        };

        if (!string.IsNullOrEmpty(impersonatedBy))
            claims.Add(new Claim("impersonatedBy", impersonatedBy));

        if (!string.IsNullOrEmpty(defaultLanguage))
            claims.Add(new Claim("default_language", defaultLanguage));

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);
        var expiry      = DateTime.UtcNow.Add(AccessTokenExpiry);

        var token = new JwtSecurityToken(
            issuer:             _issuer,
            audience:           _audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            expiry,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
