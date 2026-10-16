using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using MLMConquerorGlobalEdition.Billing.Controllers;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Services;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Tokens;

/// <summary>
/// Billing comprueba el permiso de step-up con la llave pública y lo quema con SU marca, distinta de
/// la del BizCenter: la llamada que el BizCenter reenvía pasa, y el mismo permiso no se puede volver a
/// usar llamando a Billing directamente (D13). Validar no quema; quemar es un paso aparte.
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

    private static ICacheService Cache() =>
        new CacheService(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

    private static StepUpGrantCheck Check(ICacheService? cache = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"]          = Issuer,
            ["Jwt:Audience"]        = Audience,
            ["Jwt:PublicKeyBase64"] = Convert.ToBase64String(Llave.ExportSubjectPublicKeyInfo())
        }).Build();
        return new StepUpGrantCheck(new StepUpGrantValidator(config, new SharedClockAdapter(new RelojFijo())), cache ?? Cache());
    }

    private static string Permiso(string userId, string operation, DateTime? expira = null, string? jti = null)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Jti, jti ?? Guid.NewGuid().ToString("N")),
            new Claim(ChallengeAudience.PurposeClaim, StepUpGrant.PurposeFor(operation)),
            new Claim(StepUpGrant.OperationClaim, operation)
        };
        var token = new JwtSecurityToken(
            Issuer, StepUpGrant.AudienceFor(Audience), claims,
            notBefore: Ahora.AddSeconds(-5), expires: expira ?? Ahora.Add(StepUpGrant.Lifetime),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(Llave), SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool Vale(StepUpGrantCheck check, string? permiso, string userId, string operacion) =>
        check.ValidJti(permiso, userId, operacion) is not null;

    // ─── Validar ─────────────────────────────────────────────────────────────

    [Fact]
    public void Un_permiso_bueno_da_su_jti_y_validar_no_lo_quema()
    {
        var check   = Check();
        var permiso = Permiso("user-001", StepUpOperations.TokenPurchaseFromBalance, jti: "jti-1");

        check.ValidJti(permiso, "user-001", StepUpOperations.TokenPurchaseFromBalance).Should().Be("jti-1");
        check.ValidJti(permiso, "user-001", StepUpOperations.TokenPurchaseFromBalance).Should().Be("jti-1");
    }

    [Fact]
    public void De_otro_usuario_no_vale()
        => Vale(Check(), Permiso("user-002", StepUpOperations.TokenPurchaseFromBalance), "user-001",
                StepUpOperations.TokenPurchaseFromBalance).Should().BeFalse();

    [Fact]
    public void De_otra_operacion_no_vale()
        => Vale(Check(), Permiso("user-001", StepUpOperations.PayoutMethodChange), "user-001",
                StepUpOperations.TokenPurchaseFromBalance).Should().BeFalse();

    [Fact]
    public void Vencido_no_vale()
        => Vale(Check(), Permiso("user-001", StepUpOperations.TokenPurchaseFromBalance, expira: Ahora.AddSeconds(-1)),
                "user-001", StepUpOperations.TokenPurchaseFromBalance).Should().BeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-un-jwt")]
    public void Sin_permiso_o_basura_no_vale(string? permiso)
        => Vale(Check(), permiso, "user-001", StepUpOperations.TokenPurchaseFromBalance).Should().BeFalse();

    [Fact]
    public void Sin_usuario_no_vale()
        => Vale(Check(), Permiso("user-001", StepUpOperations.TokenPurchaseFromBalance), "",
                StepUpOperations.TokenPurchaseFromBalance).Should().BeFalse();

    // ─── Quemar ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quemar_vale_una_sola_vez()
    {
        var check = Check();

        (await check.TryConsumeAsync("jti-1")).Should().BeTrue();
        (await check.TryConsumeAsync("jti-1")).Should().BeFalse();
    }

    [Fact]
    public async Task La_marca_del_BizCenter_no_impide_la_de_Billing()
    {
        var cache = Cache();
        (await cache.TryClaimOnceAsync(CacheKeys.StepUpGrantConsumed("jti-1"), TimeSpan.FromMinutes(3))).Should().BeTrue();

        (await Check(cache).TryConsumeAsync("jti-1")).Should().BeTrue();
        CacheKeys.BillingStepUpGrantConsumed("jti-1").Should().NotBe(CacheKeys.StepUpGrantConsumed("jti-1"));
    }

    [Fact]
    public async Task Si_la_cache_no_responde_quemar_lanza_y_no_dice_que_no_se_uso()
    {
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.TryClaimOnceAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("redis caído"));

        var act = () => Check(cache.Object).TryConsumeAsync("jti-1");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task La_marca_dura_mas_que_el_permiso()
    {
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.TryClaimOnceAsync(CacheKeys.BillingStepUpGrantConsumed("jti-1"), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);

        await Check(cache.Object).TryConsumeAsync("jti-1");

        cache.Verify(c => c.TryClaimOnceAsync(CacheKeys.BillingStepUpGrantConsumed("jti-1"),
            It.Is<TimeSpan>(t => t > StepUpGrant.Lifetime), It.IsAny<CancellationToken>()));
    }

    // ─── De punta a punta en el controlador (permiso real, caché real) ───────

    private static TokenPurchasesController Controlador(StepUpGrantCheck check, Mock<ITokenPurchaseService> svc, string permiso)
    {
        var ctx = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-001"),
                new Claim(CallerIdentity.MemberIdClaim, "AMB-100001")
            }, "prueba"))
        };
        ctx.Request.Headers[StepUpGrant.HeaderName] = permiso;
        return new TokenPurchasesController(svc.Object, check, NullLogger<TokenPurchasesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ctx }
        };
    }

    private static PurchaseTokensRequest Pedido(string clave) => new()
    {
        TokenTypeId = 13, Quantity = 1, PaymentMethod = TokenPaymentMethods.EWallet, IdempotencyKey = clave
    };

    [Fact]
    public async Task El_mismo_permiso_con_otra_clave_se_rechaza_STEP_UP_INVALID()
    {
        var check   = Check();
        var permiso = Permiso("user-001", StepUpOperations.TokenPurchaseFromBalance);
        var svc     = new Mock<ITokenPurchaseService>();
        svc.Setup(s => s.PurchaseAsync(It.IsAny<TokenPurchaseInput>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Result<TokenPurchaseResultDto>.Success(new TokenPurchaseResultDto { PurchaseId = "p1" }));

        (await Controlador(check, svc, permiso).Purchase(Pedido("clave-1"), default)).Should().BeOfType<OkObjectResult>();
        var segunda = await Controlador(check, svc, permiso).Purchase(Pedido("clave-2"), default);

        var obj = segunda.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        obj.Value.Should().BeOfType<ApiResponse<object>>().Which.ErrorCode.Should().Be(StepUpGrant.InvalidErrorCode);
        svc.Verify(s => s.PurchaseAsync(It.IsAny<TokenPurchaseInput>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Repetir_con_la_misma_clave_devuelve_la_compra_aunque_el_permiso_ya_este_quemado()
    {
        var check   = Check();
        var permiso = Permiso("user-001", StepUpOperations.TokenPurchaseFromBalance);
        var svc     = new Mock<ITokenPurchaseService>();
        svc.SetupSequence(s => s.HasPurchaseAsync("AMB-100001", "clave-1", It.IsAny<CancellationToken>()))
           .ReturnsAsync(false)  // primera vez: no hay compra con esa clave, se quema
           .ReturnsAsync(true);  // repetición: la compra ya existe
        svc.Setup(s => s.PurchaseAsync(It.IsAny<TokenPurchaseInput>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Result<TokenPurchaseResultDto>.Success(new TokenPurchaseResultDto { PurchaseId = "p1" }));

        (await Controlador(check, svc, permiso).Purchase(Pedido("clave-1"), default)).Should().BeOfType<OkObjectResult>();
        var repeticion = await Controlador(check, svc, permiso).Purchase(Pedido("clave-1"), default);

        repeticion.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ApiResponse<TokenPurchaseResultDto>>()
                  .Which.Data!.PurchaseId.Should().Be("p1");
        svc.Verify(s => s.PurchaseAsync(It.IsAny<TokenPurchaseInput>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
