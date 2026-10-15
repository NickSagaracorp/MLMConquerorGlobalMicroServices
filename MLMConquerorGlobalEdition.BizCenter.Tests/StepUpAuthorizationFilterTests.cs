using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// El filtro es la única barrera entre una sesión robada y la wallet de cobro. Cada prueba
/// comprueba que la petición NO llega a la acción (<c>context.Result</c> queda puesto), salvo las
/// del permiso bueno.
/// </summary>
public class StepUpAuthorizationFilterTests
{
    private const string Issuer   = "MLMConqueror";
    private const string Audience = "MLMConquerorUsers";
    private const string UserId   = "user-001";
    private const string Op       = StepUpOperations.PayoutMethodChange;

    private static readonly DateTime FixedNow = new(2026, 10, 14, 10, 0, 0, DateTimeKind.Utc);

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly HashSet<string> _claimed = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<IDateTimeProvider> _clock = new();

    public StepUpAuthorizationFilterTests()
    {
        _clock.Setup(c => c.UtcNow).Returns(FixedNow);
        _clock.Setup(c => c.Now).Returns(FixedNow);
        // SET NX en miniatura: true la primera vez que se ve la clave, false las demás.
        _cache.Setup(c => c.TryClaimOnceAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((string k, TimeSpan _, CancellationToken _) => _claimed.Add(k));
    }

    private IConfiguration Config() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:PublicKeyBase64"] = Convert.ToBase64String(_rsa.ExportSubjectPublicKeyInfo()),
            ["Jwt:Issuer"]          = Issuer,
            ["Jwt:Audience"]        = Audience
        }).Build();

    private StepUpAuthorizationFilter Filter() =>
        new(Op, new StepUpGrantValidator(Config(), _clock.Object), _cache.Object,
            NullLogger<StepUpAuthorizationFilter>.Instance);

    /// <summary>Firma un permiso igual que StepUpGrantService, sin depender de Authn.</summary>
    private string Grant(
        string    userId    = UserId,
        string    op        = Op,
        string?   jti       = null,
        DateTime? now       = null,
        DateTime? notBefore = null,
        RSA?      key       = null,
        string?   audience  = null,
        bool      asAccess  = false)
    {
        var issuedAt = now ?? FixedNow;
        var claims = new List<Claim>
        {
            new("sub", userId),
            new("jti", jti ?? Guid.NewGuid().ToString())
        };
        if (!asAccess)
        {
            claims.Add(new Claim("purpose", StepUpGrant.PurposeFor(op)));
            claims.Add(new Claim(StepUpGrant.OperationClaim, op));
        }

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: audience ?? (asAccess ? Audience : StepUpGrant.AudienceFor(Audience)),
            claims: claims,
            notBefore: notBefore ?? issuedAt, expires: issuedAt.Add(StepUpGrant.Lifetime),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(key ?? _rsa), SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Bearer"));

    private static AuthorizationFilterContext Context(
        StringValues? header, string userId = UserId, ClaimsPrincipal? user = null)
    {
        var http = new DefaultHttpContext
        {
            User = user ?? Principal(new Claim(ClaimTypes.NameIdentifier, userId)),
            TraceIdentifier = "trace-123"
        };
        if (header is not null) http.Request.Headers[StepUpGrant.HeaderName] = header.Value;
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(action, new List<IFilterMetadata>());
    }

    private static ApiResponse<StepUpRequiredData>? Body(AuthorizationFilterContext ctx) =>
        (ctx.Result as ObjectResult)?.Value as ApiResponse<StepUpRequiredData>;

    private static string? ErrorCode(AuthorizationFilterContext ctx) => Body(ctx)?.ErrorCode;

    private void ClaimWasNeverAttempted() =>
        _cache.Verify(c => c.TryClaimOnceAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
                      Times.Never);

    // ── lo de siempre ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sin_cabecera_corta_con_403_STEP_UP_REQUIRED_y_dice_que_operacion()
    {
        var ctx = Context(header: null);
        await Filter().OnAuthorizationAsync(ctx);

        var result = ctx.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(403);
        var body = result.Value.Should().BeOfType<ApiResponse<StepUpRequiredData>>().Subject;
        body.ErrorCode.Should().Be(StepUpGrant.RequiredErrorCode);
        body.Data!.OperationKey.Should().Be(Op);
        body.TraceId.Should().Be("trace-123");
    }

    [Fact]
    public async Task Permiso_valido_deja_pasar()
    {
        var ctx = Context(Grant());
        await Filter().OnAuthorizationAsync(ctx);
        ctx.Result.Should().BeNull();
    }

    [Fact]
    public async Task El_mismo_permiso_dos_veces_la_segunda_se_corta()
    {
        var token = Grant();
        var first = Context(token);
        await Filter().OnAuthorizationAsync(first);
        var second = Context(token);
        await Filter().OnAuthorizationAsync(second);

        first.Result.Should().BeNull();
        ErrorCode(second).Should().Be(StepUpGrant.InvalidErrorCode);
        Body(second)!.TraceId.Should().Be("trace-123");
    }

    [Fact]
    public async Task Permiso_de_otro_usuario_se_corta()
    {
        var ctx = Context(Grant(userId: "otro-usuario"));
        await Filter().OnAuthorizationAsync(ctx);
        ErrorCode(ctx).Should().Be(StepUpGrant.InvalidErrorCode);
    }

    [Fact]
    public async Task Permiso_de_otra_operacion_se_corta()
    {
        var ctx = Context(Grant(op: "otra.operacion"));
        await Filter().OnAuthorizationAsync(ctx);
        ErrorCode(ctx).Should().Be(StepUpGrant.InvalidErrorCode);
    }

    [Fact]
    public async Task Permiso_vencido_se_corta()
    {
        var ctx = Context(Grant(now: FixedNow.AddMinutes(-3)));
        await Filter().OnAuthorizationAsync(ctx);
        ErrorCode(ctx).Should().Be(StepUpGrant.InvalidErrorCode);
    }

    [Fact]
    public async Task Basura_en_la_cabecera_se_corta()
    {
        var ctx = Context("no-es-un-jwt");
        await Filter().OnAuthorizationAsync(ctx);
        ErrorCode(ctx).Should().Be(StepUpGrant.InvalidErrorCode);
    }

    // ── lo que se parece a un permiso y no lo es ─────────────────────────────────

    [Fact]
    public async Task Un_token_de_acceso_en_la_cabecera_se_corta()
    {
        // Lo que más a mano tiene quien roba una sesión: el propio token de acceso.
        var ctx = Context(Grant(asAccess: true));
        await Filter().OnAuthorizationAsync(ctx);
        ErrorCode(ctx).Should().Be(StepUpGrant.InvalidErrorCode);
    }

    [Fact]
    public async Task Permiso_firmado_con_otra_llave_se_corta()
    {
        using var otra = RSA.Create(2048);
        var ctx = Context(Grant(key: otra));
        await Filter().OnAuthorizationAsync(ctx);
        ErrorCode(ctx).Should().Be(StepUpGrant.InvalidErrorCode);
    }

    [Fact]
    public async Task Permiso_que_aun_no_es_valido_se_corta()
    {
        var ctx = Context(Grant(notBefore: FixedNow.AddSeconds(30)));
        await Filter().OnAuthorizationAsync(ctx);
        ErrorCode(ctx).Should().Be(StepUpGrant.InvalidErrorCode);
    }

    [Fact]
    public async Task Sin_sesion_un_permiso_valido_se_corta()
    {
        var ctx = Context(Grant(), user: new ClaimsPrincipal(new ClaimsIdentity()));
        await Filter().OnAuthorizationAsync(ctx);
        ErrorCode(ctx).Should().Be(StepUpGrant.InvalidErrorCode);
    }

    [Fact]
    public async Task Con_solo_el_claim_sub_deja_pasar()
    {
        // CallerIdentity.UserIdOf acepta NameIdentifier o sub: cuál llega depende de si el mapeo
        // de claims entrantes está activo en el anfitrión, y el filtro no puede depender de eso.
        var ctx = Context(Grant(), user: Principal(new Claim("sub", UserId)));
        await Filter().OnAuthorizationAsync(ctx);
        ctx.Result.Should().BeNull();
    }

    [Fact]
    public async Task Dos_valores_en_la_cabecera_se_cortan()
    {
        // Aunque los dos sean buenos: no hay forma correcta de elegir cuál quemar.
        var ctx = Context(new StringValues(new[] { Grant(), Grant() }));
        await Filter().OnAuthorizationAsync(ctx);
        ErrorCode(ctx).Should().Be(StepUpGrant.InvalidErrorCode);
    }

    [Fact]
    public async Task Lo_rechazado_nunca_llega_a_marcar_el_permiso()
    {
        // Quemar un jti por una presentación inválida dejaría a cualquiera con un permiso ajeno
        // —leído de un log, por ejemplo— anularlo antes de que su dueño lo use.
        using var otra = RSA.Create(2048);
        var rechazados = new[]
        {
            Context(header: null),
            Context("no-es-un-jwt"),
            Context(Grant(userId: "otro-usuario")),
            Context(Grant(op: "otra.operacion")),
            Context(Grant(now: FixedNow.AddMinutes(-3))),
            Context(Grant(asAccess: true)),
            Context(Grant(key: otra)),
            Context(Grant(notBefore: FixedNow.AddSeconds(30))),
            Context(Grant(), user: new ClaimsPrincipal(new ClaimsIdentity())),
            Context(new StringValues(new[] { Grant(), Grant() }))
        };

        foreach (var ctx in rechazados)
        {
            await Filter().OnAuthorizationAsync(ctx);
            ctx.Result.Should().NotBeNull();
        }

        ClaimWasNeverAttempted();
    }

    // ── falla cerrado ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Si_el_almacen_falla_se_corta_con_403()
    {
        _cache.Setup(c => c.TryClaimOnceAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("Redis no responde"));

        var ctx = Context(Grant());
        await Filter().OnAuthorizationAsync(ctx);

        var result = ctx.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(403);
        Body(ctx)!.ErrorCode.Should().Be(StepUpGrant.InvalidErrorCode);
        Body(ctx)!.Errors.Should().ContainSingle().Which.Should().Be("No se pudo verificar el permiso.");
    }

    [Fact]
    public async Task Una_cancelacion_se_propaga_y_no_se_disfraza_de_403()
    {
        _cache.Setup(c => c.TryClaimOnceAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new OperationCanceledException());

        var act = () => Filter().OnAuthorizationAsync(Context(Grant()));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── el camino real: atributo → DI → filtro ───────────────────────────────────

    [Fact]
    public async Task El_atributo_construye_un_filtro_que_funciona_desde_DI()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Config());
        services.AddSingleton(_clock.Object);
        services.AddSingleton<StepUpGrantValidator>();
        services.AddSingleton(_cache.Object);
        services.AddLogging();
        using var sp = services.BuildServiceProvider();

        var filter = new RequiresStepUpAttribute(Op).CreateInstance(sp);

        var authz = filter.Should().BeOfType<StepUpAuthorizationFilter>().Subject;
        var ok = Context(Grant());
        await authz.OnAuthorizationAsync(ok);
        ok.Result.Should().BeNull();

        var none = Context(header: null);
        await authz.OnAuthorizationAsync(none);
        ErrorCode(none).Should().Be(StepUpGrant.RequiredErrorCode);
    }

    [Fact]
    public void El_atributo_con_una_operacion_inventada_revienta()
    {
        var act = () => new RequiresStepUpAttribute("inventada");
        act.Should().Throw<ArgumentException>();
    }
}
