using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.CuotaAnual;

/// <summary>
/// La cuota en el BizCenter (Plan K): el resumen y la preferencia se resuelven aquí con el miembro del
/// token; pagar exige step-up y se reenvía a Billing con el Bearer y el permiso, sin MemberId, y lo que
/// conteste Billing llega tal cual. Nada que escriba se hace suplantando. La tarjeta no es un método.
/// </summary>
public class CuotaAnualEnElBizCenterTests
{
    private readonly Mock<IAnnualFeeOverviewService> _overview = new();
    private readonly Mock<IBillingClient> _billing = new(MockBehavior.Strict);

    private AnnualFeeController Controlador(bool suplantando = false, string? grant = null)
    {
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.MemberId).Returns("AMB-1");
        usuario.Setup(u => u.UserId).Returns("user-001");

        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Bearer jwt.miembro";
        if (grant is not null) ctx.Request.Headers[StepUpGrant.HeaderName] = grant;
        var claims = new List<Claim> { new("memberId", "AMB-1"), new(ClaimTypes.NameIdentifier, "user-001") };
        if (suplantando) claims.Add(new Claim(ImpersonationScope.ImpersonatedByClaim, "admin-9"));
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        return new AnnualFeeController(_overview.Object, _billing.Object, usuario.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = ctx }
        };
    }

    [Fact]
    public async Task El_resumen_es_del_miembro_del_token()
    {
        _overview.Setup(o => o.GetAsync("AMB-1", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new AnnualFeeOverviewDto { Applies = true, Status = AnnualFeeStatuses.Due });

        var r = await Controlador().Get(default);

        r.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ApiResponse<AnnualFeeOverviewDto>>()
         .Which.Data!.Status.Should().Be(AnnualFeeStatuses.Due);
    }

    [Fact]
    public async Task La_preferencia_se_guarda_para_el_miembro_del_token_con_el_usuario_como_actor()
    {
        _overview.Setup(o => o.SetFeesFromBalanceAsync("AMB-1", false, "user-001", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var r = await Controlador().SetPreference(new AnnualFeePreferenceRequest { FeesFromBalance = false }, default);

        r.Should().BeOfType<OkObjectResult>();
        _overview.VerifyAll();
    }

    [Fact]
    public async Task Suplantando_no_se_cambia_la_preferencia()
    {
        var r = await Controlador(suplantando: true).SetPreference(new AnnualFeePreferenceRequest { FeesFromBalance = false }, default);

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _overview.Verify(o => o.SetFeesFromBalanceAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Pagar_reenvia_el_Bearer_y_el_permiso_a_Billing_y_devuelve_su_estado()
    {
        var pedido = new PayAnnualFeeRequest { Method = AnnualFeeMethods.Token, IdempotencyKey = "clave01" };
        _billing.Setup(b => b.PayAnnualFeeAsync(pedido, "jwt.miembro", "permiso", It.IsAny<CancellationToken>()))
                .ReturnsAsync(BillingResult<AnnualFeePaymentResultDto>.Fail(StatusCodes.Status409Conflict, AnnualFeeErrors.IdempotencyKeyReused, "x"));

        var r = await Controlador(grant: "permiso").Pay(pedido, default);

        var obj = r.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        obj.Value.Should().BeOfType<ApiResponse<AnnualFeePaymentResultDto>>().Which.ErrorCode.Should().Be(AnnualFeeErrors.IdempotencyKeyReused);
    }

    [Fact]
    public async Task Suplantando_no_se_paga_ni_se_llama_a_Billing()
    {
        var r = await Controlador(suplantando: true, grant: "permiso")
            .Pay(new PayAnnualFeeRequest { Method = AnnualFeeMethods.Token, IdempotencyKey = "clave01" }, default);

        r.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _billing.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("CreditCard")]
    [InlineData("Crypto")]
    public async Task La_tarjeta_y_la_cripto_no_pagan_la_cuota(string metodo)
    {
        var r = await Controlador(grant: "permiso").Pay(new PayAnnualFeeRequest { Method = metodo, IdempotencyKey = "clave01" }, default);

        r.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().BeOfType<ApiResponse<AnnualFeePaymentResultDto>>()
         .Which.ErrorCode.Should().Be(AnnualFeeErrors.MethodNotSupported);
        _billing.VerifyNoOtherCalls();
    }

    [Fact]
    public void Pagar_exige_step_up_de_la_cuota()
        => typeof(AnnualFeeController).GetMethod(nameof(AnnualFeeController.Pay))!
            .GetCustomAttribute<RequiresStepUpAttribute>()!.OperationKey.Should().Be(StepUpOperations.AnnualFeeFromBalance);

    // ── El cliente de Billing ────────────────────────────────────────────────

    private sealed class Manejador(Func<HttpRequestMessage, HttpResponseMessage> f) : HttpMessageHandler
    {
        public HttpRequestMessage? Ultima;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Ultima = r;
            return Task.FromResult(f(r));
        }
    }

    [Fact]
    public async Task El_cliente_manda_el_pago_a_Billing_con_el_permiso_y_lee_la_respuesta()
    {
        var h = new Manejador(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"success":true,"data":{"paymentId":"p1","status":"Completed","method":"Token"}}""", Encoding.UTF8, "application/json")
        });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(BillingClient.HttpClientName))
               .Returns(() => new HttpClient(h, disposeHandler: false) { BaseAddress = new Uri("https://billing.pruebas/") });

        var r = await new BillingClient(factory.Object, NullLogger<BillingClient>.Instance)
            .PayAnnualFeeAsync(new PayAnnualFeeRequest { Method = "Token", IdempotencyKey = "k" }, "jwt.miembro", "permiso");

        r.IsSuccess.Should().BeTrue();
        r.Value!.PaymentId.Should().Be("p1");
        h.Ultima!.Method.Should().Be(HttpMethod.Post);
        h.Ultima.RequestUri!.AbsolutePath.Should().Be("/api/v1/billing/annual-fee/pay");
        h.Ultima.Headers.Authorization!.Parameter.Should().Be("jwt.miembro");
        h.Ultima.Headers.GetValues(StepUpGrant.HeaderName).Should().Equal("permiso");
    }
}
