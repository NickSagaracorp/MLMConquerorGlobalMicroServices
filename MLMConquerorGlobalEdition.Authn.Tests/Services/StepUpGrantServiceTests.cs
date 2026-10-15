using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.Authn.Services;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;
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
            MLMConquerorGlobalEdition.Domain.Entities.Security.TwoFactorChannel.Email, "123456", Op);

        new StepUpGrantValidator(config, clock.Object).Validate(challenge, UserId, Op)
            .IsSuccess.Should().BeFalse();
    }

    // ── lo que NO es un permiso ──────────────────────────────────────────────────

    /// <summary>La llave privada de la configuración, para falsificar tokens "casi buenos".</summary>
    private static RsaSecurityKey SigningKey(IConfiguration config)
    {
        var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(config["Jwt:PrivateKeyBase64"]!), out _);
        return new RsaSecurityKey(rsa);
    }

    private static string Sign(
        SecurityKey key, string issuer, string audience, IEnumerable<Claim> claims, DateTime now)
    {
        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            notBefore:          now,
            expires:            now.Add(StepUpGrant.Lifetime),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static Claim[] GrantClaims(string? purpose, string? operation)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, UserId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (purpose is not null)   claims.Add(new Claim(ChallengeAudience.PurposeClaim, purpose));
        if (operation is not null) claims.Add(new Claim(StepUpGrant.OperationClaim, operation));
        return claims.ToArray();
    }

    [Fact]
    public void Un_token_de_acceso_no_vale_como_permiso()
    {
        // Misma llave, mismo emisor, mismo sub: lo único que lo separa es la audiencia y la falta
        // de propósito. Si esto pasara, cualquier sesión abierta cambiaría la wallet sin código.
        var config = Config(out _);
        var access = Sign(SigningKey(config), Issuer, Audience,
            new[] { new Claim(JwtRegisteredClaimNames.Sub, UserId),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) }, FixedNow);

        new StepUpGrantValidator(config, Clock(FixedNow).Object).Validate(access, UserId, Op)
            .IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Con_el_proposito_del_reto_no_valida_aunque_todo_lo_demas_cuadre()
    {
        // Audiencia de permiso, firma buena, operación buena: solo el propósito es el del reto
        // ("step_up:"). Prueba que el propósito se comprueba por sí solo.
        var config = Config(out _);
        var token  = Sign(SigningKey(config), Issuer, StepUpGrant.AudienceFor(Audience),
            GrantClaims(purpose: "step_up:" + Op, operation: Op), FixedNow);

        new StepUpGrantValidator(config, Clock(FixedNow).Object).Validate(token, UserId, Op)
            .IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Sin_el_claim_de_operacion_no_valida_aunque_el_proposito_cuadre()
    {
        // La otra mitad: propósito bueno y sin operation_key. Prueba que la operación también se
        // comprueba por sí sola, y no solo a través del propósito.
        var config = Config(out _);
        var token  = Sign(SigningKey(config), Issuer, StepUpGrant.AudienceFor(Audience),
            GrantClaims(purpose: StepUpGrant.PurposeFor(Op), operation: null), FixedNow);

        new StepUpGrantValidator(config, Clock(FixedNow).Object).Validate(token, UserId, Op)
            .IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Control_el_token_falsificado_con_todo_bueno_si_valida()
    {
        // Sin este control, las pruebas de falsificación podrían estar fallando por cualquier otra
        // razón (el ayudante de firma mal hecho) y seguirían verdes.
        var config = Config(out _);
        var token  = Sign(SigningKey(config), Issuer, StepUpGrant.AudienceFor(Audience),
            GrantClaims(purpose: StepUpGrant.PurposeFor(Op), operation: Op), FixedNow);

        new StepUpGrantValidator(config, Clock(FixedNow).Object).Validate(token, UserId, Op)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Firmado_con_otra_llave_no_valida()
    {
        var config = Config(out _);
        using var otra = RSA.Create(2048);
        var token = Sign(new RsaSecurityKey(otra), Issuer, StepUpGrant.AudienceFor(Audience),
            GrantClaims(StepUpGrant.PurposeFor(Op), Op), FixedNow);

        new StepUpGrantValidator(config, Clock(FixedNow).Object).Validate(token, UserId, Op)
            .IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Con_otro_emisor_no_valida()
    {
        var config = Config(out _);
        var token  = Sign(SigningKey(config), "OtroEmisor", StepUpGrant.AudienceFor(Audience),
            GrantClaims(StepUpGrant.PurposeFor(Op), Op), FixedNow);

        new StepUpGrantValidator(config, Clock(FixedNow).Object).Validate(token, UserId, Op)
            .IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Justo_en_el_vencimiento_ya_no_valida()
    {
        // ClockSkew es cero y la comparación es estricta: en el instante exacto de exp ya está muerto.
        var config = Config(out _);
        var token  = new StepUpGrantService(config, Clock(FixedNow).Object).Issue(UserId, Op);
        var atExp  = Clock(FixedNow.Add(StepUpGrant.Lifetime));

        new StepUpGrantValidator(config, atExp.Object).Validate(token, UserId, Op)
            .IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("inventada")]
    public void Validar_contra_una_operacion_fuera_del_catalogo_no_valida(string expected)
    {
        var config = Config(out _);
        var clock  = Clock(FixedNow);
        var token  = new StepUpGrantService(config, clock.Object).Issue(UserId, Op);

        new StepUpGrantValidator(config, clock.Object).Validate(token, UserId, expected)
            .IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Emitir_sin_usuario_revienta(string? userId)
    {
        var service = new StepUpGrantService(Config(out _), Clock(FixedNow).Object);

        var act = () => service.Issue(userId!, Op);

        act.Should().Throw<ArgumentException>();
    }

    // ── un permiso presentado como Bearer ────────────────────────────────────────

    /// <summary>
    /// El permiso lleva la llave, el emisor y el sub buenos; si un anfitrión lo aceptara como
    /// token de acceso, un permiso robado valdría una sesión. Se levanta un anfitrión con la misma
    /// configuración de bearer que los servicios reales —ver <c>RetoComoBearerTests</c>— para que
    /// el 401 sea el de la tubería y no una aserción sobre la librería.
    /// </summary>
    [Fact]
    public async Task Un_permiso_presentado_como_bearer_no_abre_un_endpoint_autorizado()
    {
        // Hora real: el middleware mide la vigencia con el reloj de la máquina, y un permiso
        // caducado daría 401 por la razón equivocada.
        var ahora   = DateTime.UtcNow;
        var config  = Config(out var publicKey);
        var permiso = new StepUpGrantService(config, Clock(ahora).Object).Issue(UserId, Op);
        var acceso  = Sign(SigningKey(config), Issuer, Audience,
            new[] { new Claim(JwtRegisteredClaimNames.Sub, UserId) }, ahora);

        var rsaValidacion = RSA.Create();
        rsaValidacion.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
        var llaveValidacion = new RsaSecurityKey(rsaValidacion);

        using var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(options =>
                        {
                            // Copia fiel de los Program.cs, como en RetoComoBearerTests.
                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer           = true,
                                ValidateAudience         = true,
                                ValidateLifetime         = true,
                                ValidateIssuerSigningKey = true,
                                ValidIssuer              = Issuer,
                                ValidAudience            = Audience,
                                IssuerSigningKey         = llaveValidacion,
                                ClockSkew                = TimeSpan.Zero
                            };
                            options.Events = new JwtBearerEvents
                            {
                                OnTokenValidated = ctx =>
                                {
                                    if (ChallengeAudience.CarriesPurpose(ctx.Principal!.Claims))
                                        ctx.Fail("Un reto de 2FA no autoriza: falta completar el segundo factor.");
                                    return Task.CompletedTask;
                                }
                            };
                        });
                    services.AddAuthorization();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(e =>
                        e.Map("/api/v1/bizcenter/wallets", () => Results.Ok("hecho")).RequireAuthorization());
                });
            })
            .StartAsync();
        using var client = host.GetTestClient();

        async Task<HttpStatusCode> Llamar(string token)
        {
            var peticion = new HttpRequestMessage(HttpMethod.Get, "/api/v1/bizcenter/wallets");
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return (await client.SendAsync(peticion)).StatusCode;
        }

        (await Llamar(permiso)).Should().Be(HttpStatusCode.Unauthorized);
        // Control: el anfitrión sí deja pasar un acceso de verdad, así que el 401 es por el permiso.
        (await Llamar(acceso)).Should().Be(HttpStatusCode.OK);

        await host.StopAsync();
    }

    [Fact]
    public void El_firmante_se_registra_como_singleton_para_importar_la_llave_una_sola_vez()
    {
        var services = new ServiceCollection();
        services.AddAuthnStepUpGrants();

        var d = services.Single(x => x.ServiceType == typeof(MLMConquerorGlobalEdition.Authn.Abstractions.IStepUpGrantService));
        d.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }
}
