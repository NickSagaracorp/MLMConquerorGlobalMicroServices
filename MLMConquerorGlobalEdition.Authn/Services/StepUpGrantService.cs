using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.Authn.Services;

/// <inheritdoc cref="IStepUpGrantService"/>
public sealed class StepUpGrantService : IStepUpGrantService
{
    private readonly RsaSecurityKey    _signingKey;
    private readonly string            _issuer;
    private readonly string            _audience;
    private readonly IDateTimeProvider _dateTime;

    public StepUpGrantService(IConfiguration config, IDateTimeProvider dateTime)
    {
        _dateTime = dateTime;
        _issuer   = config["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer not configured.");
        var accessAudience = config["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience not configured.");
        _audience = StepUpGrant.AudienceFor(accessAudience);

        var privateKeyBase64 = JwtKeyGuard.ValidatePrivateKey(config["Jwt:PrivateKeyBase64"]);
        var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
        _signingKey = new RsaSecurityKey(rsa);
    }

    public string Issue(string userId, string operationKey)
    {
        if (!StepUpOperations.IsKnown(operationKey))
            throw new ArgumentException($"Operación de step-up desconocida: {operationKey}.", nameof(operationKey));

        var now = _dateTime.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ChallengeAudience.PurposeClaim, StepUpGrant.PurposeFor(operationKey)),
            new Claim(StepUpGrant.OperationClaim, operationKey)
        };

        var token = new JwtSecurityToken(
            issuer:             _issuer,
            audience:           _audience,
            claims:             claims,
            notBefore:          now,
            expires:            now.Add(StepUpGrant.Lifetime),
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
