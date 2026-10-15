using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Authn.Services;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;

namespace MLMConquerorGlobalEdition.Authn.Tests.Services;

/// <summary>
/// El permiso lo firma Authn y lo valida SharedKernel.Server en otro servicio. Estas pruebas son
/// el contrato entre los dos: si uno cambia audiencia, propósito o claims, se rompen aquí.
/// </summary>
public class StepUpGrantServiceTests
{
    private const string Issuer   = "MLMConqueror";
    private const string Audience = "MLMConquerorUsers";
    private const string UserId   = "user-001";
    private const string Op       = StepUpOperations.PayoutMethodChange;

    private static readonly DateTime FixedNow = new(2026, 10, 14, 10, 0, 0, DateTimeKind.Utc);

    private static IConfiguration Config(out string publicKey)
    {
        using var rsa = RSA.Create(2048);
        publicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:PrivateKeyBase64"] = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()),
            ["Jwt:PublicKeyBase64"]  = publicKey,
            ["Jwt:Issuer"]           = Issuer,
            ["Jwt:Audience"]         = Audience
        }).Build();
    }

    private static Mock<IDateTimeProvider> Clock(DateTime now)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(now);
        clock.Setup(c => c.Now).Returns(now);
        return clock;
    }

    [Fact]
    public void Permiso_recien_firmado_valida_para_su_usuario_y_operacion()
    {
        var config    = Config(out _);
        var clock     = Clock(FixedNow);
        var token     = new StepUpGrantService(config, clock.Object).Issue(UserId, Op);
        var validator = new StepUpGrantValidator(config, clock.Object);

        var result = validator.Validate(token, UserId, Op);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrWhiteSpace(); // el jti
    }

    [Fact]
    public void Permiso_no_sirve_para_otro_usuario()
    {
        var config = Config(out _);
        var clock  = Clock(FixedNow);
        var token  = new StepUpGrantService(config, clock.Object).Issue(UserId, Op);

        new StepUpGrantValidator(config, clock.Object).Validate(token, "otro-usuario", Op)
            .IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Permiso_no_sirve_para_otra_operacion()
    {
        var config = Config(out _);
        var clock  = Clock(FixedNow);
        var token  = new StepUpGrantService(config, clock.Object).Issue(UserId, Op);

        new StepUpGrantValidator(config, clock.Object).Validate(token, UserId, "otra.operacion")
            .IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Permiso_vencido_no_valida()
    {
        var config = Config(out _);
        var token  = new StepUpGrantService(config, Clock(FixedNow).Object).Issue(UserId, Op);
        var later  = Clock(FixedNow.Add(StepUpGrant.Lifetime).AddSeconds(1));

        new StepUpGrantValidator(config, later.Object).Validate(token, UserId, Op)
            .IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Permiso_no_lleva_la_audiencia_de_acceso_y_si_el_claim_de_proposito()
    {
        var config = Config(out _);
        var token  = new StepUpGrantService(config, Clock(FixedNow).Object).Issue(UserId, Op);
        var jwt    = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Audiences.Should().ContainSingle().Which.Should().Be(StepUpGrant.AudienceFor(Audience));
        jwt.Claims.Should().Contain(c => c.Type == "purpose" && c.Value == StepUpGrant.PurposeFor(Op));
    }

    [Fact]
    public void Un_reto_de_step_up_no_vale_como_permiso()
    {
        // El reto se emite antes de verificar nada; si valiera como permiso, pedir el código
        // bastaría para cambiar la wallet sin saberlo.
        var config    = Config(out _);
        var clock     = Clock(FixedNow);
        var challenge = new ChallengeTokenService(config, clock.Object).Issue(
            UserId, "u@d.com", MLMConquerorGlobalEdition.Domain.Entities.Security.TwoFactorPurpose.StepUp,
            MLMConquerorGlobalEdition.Domain.Entities.Security.TwoFactorChannel.Email, "hash", Op);

        new StepUpGrantValidator(config, clock.Object).Validate(challenge, UserId, Op)
            .IsSuccess.Should().BeFalse();
    }
}
