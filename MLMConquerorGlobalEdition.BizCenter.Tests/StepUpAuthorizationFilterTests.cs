using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// El filtro es la única barrera entre una sesión robada y la wallet de cobro. Cada prueba
/// comprueba que la petición NO llega a la acción (<c>context.Result</c> queda puesto), salvo la
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
    private readonly Dictionary<string, long> _counters = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<IDateTimeProvider> _clock = new();

    public StepUpAuthorizationFilterTests()
    {
        _clock.Setup(c => c.UtcNow).Returns(FixedNow);
        _clock.Setup(c => c.Now).Returns(FixedNow);
        _cache.Setup(c => c.IncrementAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((string k, TimeSpan _, CancellationToken _) =>
                  _counters[k] = _counters.GetValueOrDefault(k) + 1);
    }

    private StepUpAuthorizationFilter Filter()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:PublicKeyBase64"] = Convert.ToBase64String(_rsa.ExportSubjectPublicKeyInfo()),
            ["Jwt:Issuer"]          = Issuer,
            ["Jwt:Audience"]        = Audience
        }).Build();
        return new StepUpAuthorizationFilter(Op, new StepUpGrantValidator(config, _clock.Object), _cache.Object);
    }

    /// <summary>Firma un permiso igual que StepUpGrantService, sin depender de Authn.</summary>
    private string Grant(string userId = UserId, string op = Op, string? jti = null, DateTime? now = null)
    {
        var issuedAt = now ?? FixedNow;
        var token = new JwtSecurityToken(
            issuer: Issuer, audience: StepUpGrant.AudienceFor(Audience),
            claims: new[]
            {
                new Claim("sub", userId),
                new Claim("jti", jti ?? Guid.NewGuid().ToString()),
                new Claim("purpose", StepUpGrant.PurposeFor(op)),
                new Claim(StepUpGrant.OperationClaim, op)
            },
            notBefore: issuedAt, expires: issuedAt.Add(StepUpGrant.Lifetime),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static AuthorizationFilterContext Context(string? header, string userId = UserId)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "Bearer"))
        };
        if (header is not null) http.Request.Headers[StepUpGrant.HeaderName] = header;
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(action, new List<IFilterMetadata>());
    }

    private static string? ErrorCode(AuthorizationFilterContext ctx) =>
        ((ctx.Result as ObjectResult)?.Value as ApiResponse<StepUpRequiredData>)?.ErrorCode;

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
}
