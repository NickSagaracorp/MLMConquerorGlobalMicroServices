using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

public sealed class TwoFactorChallengeService : ITwoFactorChallengeService
{
    private const string PurposeClaim   = "purpose";
    private const string PurposeValue   = "2fa-challenge";
    private const string CodeHashClaim  = "code_hash";

    private readonly RsaSecurityKey      _signingKey;
    private readonly RsaSecurityKey      _validationKey;
    private readonly string              _issuer;
    private readonly string              _audience;
    private readonly IDateTimeProvider   _dateTime;

    public TwoFactorChallengeService(IConfiguration config, IDateTimeProvider dateTime)
    {
        _dateTime = dateTime;
        _issuer   = config["Jwt:Issuer"]   ?? throw new InvalidOperationException("Jwt:Issuer not configured.");
        _audience = config["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience not configured.");

        var privateKeyBase64 = config["Jwt:PrivateKeyBase64"]
            ?? throw new InvalidOperationException("Jwt:PrivateKeyBase64 not configured.");
        var rsaPrivate = RSA.Create();
        rsaPrivate.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
        _signingKey = new RsaSecurityKey(rsaPrivate);

        var publicKeyBase64 = config["Jwt:PublicKeyBase64"]
            ?? throw new InvalidOperationException("Jwt:PublicKeyBase64 not configured.");
        var rsaPublic = RSA.Create();
        rsaPublic.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
        _validationKey = new RsaSecurityKey(rsaPublic);

        ChallengeLifetime = TimeSpan.FromMinutes(
            config.GetValue("Auth:TwoFactor:ChallengeLifetimeMinutes", 5));
        ResendGraceWindow = TimeSpan.FromMinutes(
            config.GetValue("Auth:TwoFactor:ResendGraceWindowMinutes", 30));
    }

    public TimeSpan ChallengeLifetime { get; }
    public TimeSpan ResendGraceWindow { get; }

    public string GenerateCode()
    {
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        var num = BitConverter.ToUInt32(bytes) % 1_000_000;
        return num.ToString("D6");
    }

    public string HashCode(string code)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(bytes);
    }

    public string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0) return email;
        var local  = email[..atIndex];
        var domain = email[(atIndex + 1)..];
        var visible = local.Length <= 1 ? local : local[..1];
        return $"{visible}{new string('*', Math.Max(1, local.Length - 1))}@{domain}";
    }

    public string IssueChallenge(string userId, string email, string codeHash)
    {
        var now    = _dateTime.Now;
        var expiry = now.Add(ChallengeLifetime);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   userId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new Claim(PurposeClaim,                  PurposeValue),
            new Claim(CodeHashClaim,                 codeHash),
        };

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            issuer:             _issuer,
            audience:           _audience,
            claims:             claims,
            notBefore:          now,
            expires:            expiry,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public Result<TwoFactorChallengeClaims> ValidateChallenge(string challengeToken, bool allowExpired = false)
    {
        if (string.IsNullOrWhiteSpace(challengeToken))
            return Result<TwoFactorChallengeClaims>.Failure("INVALID_CHALLENGE", "Challenge token missing.");

        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = !allowExpired,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = _issuer,
            ValidAudience            = _audience,
            IssuerSigningKey         = _validationKey,
            ClockSkew                = TimeSpan.Zero
        };

        ClaimsPrincipal principal;
        SecurityToken   validatedToken;
        try
        {
            principal = handler.ValidateToken(challengeToken, parameters, out validatedToken);
        }
        catch (SecurityTokenExpiredException)
        {
            return Result<TwoFactorChallengeClaims>.Failure("CODE_EXPIRED", "The verification code has expired.");
        }
        catch
        {
            return Result<TwoFactorChallengeClaims>.Failure("INVALID_CHALLENGE", "Challenge token is invalid.");
        }

        var purpose = principal.FindFirst(PurposeClaim)?.Value;
        if (!string.Equals(purpose, PurposeValue, StringComparison.Ordinal))
            return Result<TwoFactorChallengeClaims>.Failure("INVALID_CHALLENGE", "Challenge token is invalid.");

        var jwt = (JwtSecurityToken)validatedToken;
        var iat = jwt.ValidFrom;
        var exp = jwt.ValidTo;

        if (allowExpired && _dateTime.Now - iat > ResendGraceWindow)
            return Result<TwoFactorChallengeClaims>.Failure("CODE_EXPIRED", "Challenge is too old to resend; please log in again.");

        var userId   = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var email    = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        var codeHash = principal.FindFirst(CodeHashClaim)?.Value;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(codeHash))
            return Result<TwoFactorChallengeClaims>.Failure("INVALID_CHALLENGE", "Challenge token is malformed.");

        return Result<TwoFactorChallengeClaims>.Success(
            new TwoFactorChallengeClaims(userId, email, codeHash, iat, exp));
    }
}
