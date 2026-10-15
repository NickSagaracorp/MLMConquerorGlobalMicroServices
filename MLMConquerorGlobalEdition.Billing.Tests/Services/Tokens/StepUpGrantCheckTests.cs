using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Tokens;

/// <summary>
/// Billing comprueba el permiso de step-up con la llave pública y SIN quemarlo: el BizCenter ya lo
/// marcó como usado, y aquí tiene que seguir valiendo para esa misma llamada (D13).
/// </summary>
public class StepUpGrantCheckTests
{
    private const string Issuer   = "MLMConquerorGlobalEdition";
    private const string Audience = "MLMConquerorGlobalEdition.Clients";

    private static readonly RSA Llave = RSA.Create(2048);
    private static readonly DateTime Ahora = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    private sealed class RelojFijo : IDateTimeProvider
    {
        public DateTime Now    => Ahora.ToLocalTime();
        public DateTime UtcNow => Ahora;
    }

    private static StepUpGrantCheck Check()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"]          = Issuer,
            ["Jwt:Audience"]        = Audience,
            ["Jwt:PublicKeyBase64"] = Convert.ToBase64String(Llave.ExportSubjectPublicKeyInfo())
        }).Build();
        return new StepUpGrantCheck(new StepUpGrantValidator(config, new SharedClockAdapter(new RelojFijo())));
    }

    private static string Permiso(string userId, string operation, DateTime? expira = null)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(ChallengeAudience.PurposeClaim, StepUpGrant.PurposeFor(operation)),
            new Claim(StepUpGrant.OperationClaim, operation)
        };
        var token = new JwtSecurityToken(
            Issuer, StepUpGrant.AudienceFor(Audience), claims,
            notBefore: Ahora.AddSeconds(-5), expires: expira ?? Ahora.Add(StepUpGrant.Lifetime),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(Llave), SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public void Un_permiso_bueno_vale_y_sigue_valiendo_porque_aqui_no_se_quema()
    {
        var check   = Check();
        var permiso = Permiso("user-001", StepUpOperations.TokenPurchaseFromBalance);

        check.IsValid(permiso, "user-001", StepUpOperations.TokenPurchaseFromBalance).Should().BeTrue();
        check.IsValid(permiso, "user-001", StepUpOperations.TokenPurchaseFromBalance).Should().BeTrue();
    }

    [Fact]
    public void De_otro_usuario_no_vale()
        => Check().IsValid(Permiso("user-002", StepUpOperations.TokenPurchaseFromBalance), "user-001",
                           StepUpOperations.TokenPurchaseFromBalance).Should().BeFalse();

    [Fact]
    public void De_otra_operacion_no_vale()
    {
        Check().IsValid(Permiso("user-001", StepUpOperations.PayoutMethodChange), "user-001", StepUpOperations.TokenPurchaseFromBalance)
               .Should().BeFalse();
    }

    [Fact]
    public void Vencido_no_vale()
        => Check().IsValid(Permiso("user-001", StepUpOperations.TokenPurchaseFromBalance, expira: Ahora.AddSeconds(-1)),
                           "user-001", StepUpOperations.TokenPurchaseFromBalance).Should().BeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-un-jwt")]
    public void Sin_permiso_o_basura_no_vale(string? permiso)
        => Check().IsValid(permiso, "user-001", StepUpOperations.TokenPurchaseFromBalance).Should().BeFalse();

    [Fact]
    public void Sin_usuario_no_vale()
        => Check().IsValid(Permiso("user-001", StepUpOperations.TokenPurchaseFromBalance), "",
                           StepUpOperations.TokenPurchaseFromBalance).Should().BeFalse();
}
