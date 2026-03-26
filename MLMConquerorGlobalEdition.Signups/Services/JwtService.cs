using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Signups.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _config;
    private readonly string _key;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtService(IConfiguration config)
    {
        _config   = config;
        _key      = config["Jwt:Key"]      ?? throw new InvalidOperationException("Jwt:Key not configured.");
        _issuer   = config["Jwt:Issuer"]   ?? throw new InvalidOperationException("Jwt:Issuer not configured.");
        _audience = config["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience not configured.");
    }

    public TimeSpan AccessTokenExpiry  => TimeSpan.FromMinutes(_config.GetValue("Jwt:AccessTokenExpiryMinutes", 60));
    public TimeSpan RefreshTokenExpiry => TimeSpan.FromDays(_config.GetValue("Jwt:RefreshTokenExpiryDays", 30));

    public string GenerateAccessToken(
        string userId,
        string memberId,
        string email,
        IEnumerable<string> roles,
        bool isImpersonating = false,
        string? impersonatedBy = null)
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

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
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
