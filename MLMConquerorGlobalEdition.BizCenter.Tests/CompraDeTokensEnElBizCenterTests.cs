using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using FluentValidation.TestHelper;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens.Validators;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetTokenSummary;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;
using IDateTimeProvider   = MLMConquerorGlobalEdition.BizCenter.Services.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// El BizCenter es un paso hacia Billing: reenvía el Bearer del miembro tal cual, no añade MemberId,
/// exige step-up en la ruta de pago con saldo (y no deja pagar con saldo por la otra), devuelve el
/// estado y el código de Billing tal cual y convierte una caída de Billing en un 502 con un código que
/// la pantalla sabe traducir. El resumen es local: solo lee.
/// </summary>
public class CompraDeTokensEnElBizCenterTests
{
    private sealed class Manejador(Func<HttpRequestMessage, HttpResponseMessage> f) : HttpMessageHandler
    {
        public HttpRequestMessage? Ultima;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Ultima = r;
            return Task.FromResult(f(r));
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode s, string json) =>
        new(s) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static BillingClient Cliente(HttpMessageHandler h)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(BillingClient.HttpClientName))
               .Returns(() => new HttpClient(h, disposeHandler: false) { BaseAddress = new Uri("https://billing.pruebas/") });
        return new BillingClient(factory.Object, NullLogger<BillingClient>.Instance);
    }

    private static DefaultHttpContext Contexto(string? grant = null, bool suplantando = false)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Authorization = "Bearer jwt.miembro";
        if (grant is not null) ctx.Request.Headers[StepUpGrant.HeaderName] = grant;
        var claims = new List<Claim> { new("memberId", "AMB-1"), new(ClaimTypes.NameIdentifier, "user-001") };
        if (suplantando) claims.Add(new Claim(ImpersonationScope.ImpersonatedByClaim, "admin-9"));
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        return ctx;
    }

    private static TokensController Controlador(Mock<IBillingClient> billing, string? grant = null, bool suplantando = false) =>
        new(Mock.Of<IMediator>(), billing.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = Contexto(grant, suplantando) }
        };

    private static PurchaseTokensRequest Peticion(string metodo) => new()
    {
        TokenTypeId = 13, Quantity = 2, PaymentMethod = metodo, IdempotencyKey = "clave01",
        CreditCardId = metodo == TokenPaymentMethods.CreditCard ? "card-1" : null,
        CryptoCurrency = metodo == TokenPaymentMethods.Crypto ? "USDT" : null
    };

    private static ObjectResult Objeto(IActionResult r) => r.Should().BeAssignableTo<ObjectResult>().Subject;

    private static ApiResponse<T> Cuerpo<T>(IActionResult r) =>
        Objeto(r).Value.Should().BeOfType<ApiResponse<T>>().Subject;

    // ─── Cliente ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task El_cliente_reenvia_el_Bearer_y_lee_el_catalogo_con_la_tarjeta_apagada()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.OK,
            """{"success":true,"data":{"monthlyLimit":50,"purchasedThisMonth":3,"monthlyRemaining":47,"items":[],"cryptoDeposits":[],"cardEnabled":false}}"""));

        var r = await Cliente(h).GetTokenCatalogAsync("jwt.del.miembro");

        r.IsSuccess.Should().BeTrue();
        r.Value!.MonthlyRemaining.Should().Be(47);
        r.Value.CardEnabled.Should().BeFalse();
        h.Ultima!.Method.Should().Be(HttpMethod.Get);
        h.Ultima.RequestUri!.AbsolutePath.Should().Be("/api/v1/billing/tokens/catalog");
        h.Ultima.Headers.Authorization!.ToString().Should().Be("Bearer jwt.del.miembro");
    }

    [Fact]
    public async Task El_cliente_lee_los_fondos_con_la_tarjeta_encendida_y_sin_saldo_eWallet()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.OK,
            """{"success":true,"data":{"commissionBalance":12.5,"eWalletBalance":null,"hasEWallet":true,"cardEnabled":true}}"""));

        var r = await Cliente(h).GetPurchaseFundsAsync("jwt");

        r.Value!.CommissionBalance.Should().Be(12.5m);
        r.Value.EWalletBalance.Should().BeNull();
        r.Value.CardEnabled.Should().BeTrue();
        h.Ultima!.RequestUri!.AbsolutePath.Should().Be("/api/v1/billing/tokens/funds");
    }

    [Fact]
    public async Task El_cliente_manda_el_permiso_de_step_up_si_lo_hay()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.OK, """{"success":true,"data":{"purchaseId":"p1"}}"""));

        var r = await Cliente(h).PurchaseTokensAsync(Peticion(TokenPaymentMethods.EWallet), "jwt", "permiso");

        r.Value!.PurchaseId.Should().Be("p1");
        h.Ultima!.Method.Should().Be(HttpMethod.Post);
        h.Ultima.RequestUri!.AbsolutePath.Should().Be("/api/v1/billing/tokens/purchase");
        h.Ultima.Headers.GetValues(StepUpGrant.HeaderName).Single().Should().Be("permiso");
    }

    [Fact]
    public async Task El_cliente_no_manda_cabecera_de_step_up_sin_permiso()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.OK, """{"success":true,"data":{"purchaseId":"p1"}}"""));

        await Cliente(h).PurchaseTokensAsync(Peticion(TokenPaymentMethods.Crypto), "jwt");

        h.Ultima!.Headers.Contains(StepUpGrant.HeaderName).Should().BeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, TokenPurchaseErrors.MonthlyLimitReached)]
    [InlineData(HttpStatusCode.BadRequest, TokenPurchaseErrors.CardPaymentsDisabled)]
    [InlineData(HttpStatusCode.BadRequest, TokenPurchaseErrors.PaymentServiceUnavailable)]
    [InlineData(HttpStatusCode.Conflict, TokenPurchaseErrors.IdempotencyKeyReused)]
    [InlineData(HttpStatusCode.Forbidden, TokenPurchaseErrors.Impersonation)]
    [InlineData(HttpStatusCode.Forbidden, StepUpGrant.RequiredErrorCode)]
    [InlineData(HttpStatusCode.NotFound, TokenPurchaseErrors.PurchaseNotFound)]
    public async Task El_cliente_conserva_el_estado_y_el_codigo_de_Billing(HttpStatusCode estado, string codigo)
    {
        var h = new Manejador(_ => Json(estado,
            $$"""{"success":false,"errorCode":"{{codigo}}","errors":["detalle de Billing"]}"""));

        var r = await Cliente(h).PurchaseTokensAsync(Peticion(TokenPaymentMethods.Crypto), "jwt");

        r.IsSuccess.Should().BeFalse();
        r.StatusCode.Should().Be((int)estado);
        r.ErrorCode.Should().Be(codigo);
        r.Error.Should().Be("detalle de Billing");
    }

    [Fact]
    public async Task Si_Billing_no_responde_es_502_BILLING_UNAVAILABLE()
    {
        var r = await Cliente(new Manejador(_ => throw new HttpRequestException("caido"))).GetPurchaseFundsAsync("jwt");

        r.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        r.ErrorCode.Should().Be(TokenPurchaseErrors.BillingUnavailable);
    }

    [Fact]
    public async Task Si_Billing_no_contesta_a_tiempo_es_502_BILLING_UNAVAILABLE()
    {
        var r = await Cliente(new Manejador(_ => throw new TaskCanceledException("timeout")))
            .PurchaseTokensAsync(Peticion(TokenPaymentMethods.Crypto), "jwt");

        r.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        r.ErrorCode.Should().Be(TokenPurchaseErrors.BillingUnavailable);
    }

    [Fact]
    public async Task Un_500_de_Billing_es_502_BILLING_UNAVAILABLE_porque_no_se_sabe_si_cobro()
    {
        var r = await Cliente(new Manejador(_ => Json(HttpStatusCode.InternalServerError,
                """{"success":false,"errorCode":"INTERNAL_ERROR","errors":["boom"]}""")))
            .PurchaseTokensAsync(Peticion(TokenPaymentMethods.Crypto), "jwt");

        r.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        r.ErrorCode.Should().Be(TokenPurchaseErrors.BillingUnavailable);
    }

    [Fact]
    public async Task Un_200_ilegible_es_502_BILLING_UNAVAILABLE()
    {
        var r = await Cliente(new Manejador(_ => Json(HttpStatusCode.OK, "<html>proxy</html>")))
            .PurchaseTokensAsync(Peticion(TokenPaymentMethods.Crypto), "jwt");

        r.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        r.ErrorCode.Should().Be(TokenPurchaseErrors.BillingUnavailable);
    }

    [Fact]
    public async Task Sin_Bearer_no_llama()
    {
        var h = new Manejador(_ => throw new InvalidOperationException("no debía llamar"));

        var r = await Cliente(h).GetTokenCatalogAsync("");

        r.ErrorCode.Should().Be("AUTH_TOKEN_MISSING");
        r.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        h.Ultima.Should().BeNull();
    }

    [Fact]
    public async Task Cancelar_escapa_el_id_de_la_compra()
    {
        var h = new Manejador(_ => Json(HttpStatusCode.OK, """{"success":true,"data":{"purchaseId":"x","status":"Cancelled"}}"""));

        var r = await Cliente(h).CancelTokenPurchaseAsync("A/../B", "jwt");

        r.Value!.Status.Should().Be(TokenPurchaseStatuses.Cancelled);
        h.Ultima!.Method.Should().Be(HttpMethod.Post);
        h.Ultima.RequestUri!.AbsolutePath.Should().Be("/api/v1/billing/tokens/purchases/A%2F..%2FB/cancel");
    }

    // ─── Controlador ─────────────────────────────────────────────────────────

    [Fact]
    public void Solo_la_ruta_de_saldo_exige_step_up_y_con_su_operacion()
    {
        typeof(TokensController).GetMethod(nameof(TokensController.PurchaseFromBalance))!
            .GetCustomAttribute<RequiresStepUpAttribute>()!.OperationKey.Should().Be(StepUpOperations.TokenPurchaseFromBalance);
        typeof(TokensController).GetMethod(nameof(TokensController.Purchase))!
            .GetCustomAttribute<RequiresStepUpAttribute>().Should().BeNull();
    }

    [Theory]
    [InlineData(TokenPaymentMethods.CommissionBalance)]
    [InlineData(TokenPaymentMethods.EWallet)]
    [InlineData("ewallet")]
    [InlineData(" EWallet ")]
    public async Task Pagar_con_saldo_por_la_ruta_sin_step_up_es_400(string metodo)
    {
        var billing = new Mock<IBillingClient>(MockBehavior.Strict);

        var r = await Controlador(billing).Purchase(Peticion(metodo), default);

        r.Should().BeOfType<BadRequestObjectResult>();
        Cuerpo<TokenPurchaseResultDto>(r).ErrorCode.Should().Be(TokenPurchaseErrors.WrongEndpoint);
    }

    [Theory]
    [InlineData(TokenPaymentMethods.CreditCard)]
    [InlineData(TokenPaymentMethods.Crypto)]
    public async Task La_ruta_de_saldo_no_acepta_tarjeta_ni_cripto(string metodo)
    {
        var billing = new Mock<IBillingClient>(MockBehavior.Strict);

        var r = await Controlador(billing, grant: "permiso").PurchaseFromBalance(Peticion(metodo), default);

        r.Should().BeOfType<BadRequestObjectResult>();
        Cuerpo<TokenPurchaseResultDto>(r).ErrorCode.Should().Be(TokenPurchaseErrors.WrongEndpoint);
    }

    [Fact]
    public async Task La_ruta_de_saldo_reenvia_el_permiso_y_el_Bearer()
    {
        var billing = new Mock<IBillingClient>();
        billing.Setup(b => b.PurchaseTokensAsync(It.IsAny<PurchaseTokensRequest>(), "jwt.miembro", "permiso", It.IsAny<CancellationToken>()))
               .ReturnsAsync(BillingResult<TokenPurchaseResultDto>.Ok(new TokenPurchaseResultDto { PurchaseId = "p1" }));

        var r = await Controlador(billing, grant: "permiso")
            .PurchaseFromBalance(Peticion(TokenPaymentMethods.EWallet), default);

        r.Should().BeOfType<OkObjectResult>();
        Cuerpo<TokenPurchaseResultDto>(r).Data!.PurchaseId.Should().Be("p1");
        billing.VerifyAll();
    }

    [Fact]
    public async Task La_ruta_normal_no_reenvia_ningun_permiso()
    {
        var billing = new Mock<IBillingClient>();
        billing.Setup(b => b.PurchaseTokensAsync(It.IsAny<PurchaseTokensRequest>(), "jwt.miembro", null, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BillingResult<TokenPurchaseResultDto>.Ok(new TokenPurchaseResultDto { PurchaseId = "p2" }));

        var r = await Controlador(billing, grant: "permiso-colado").Purchase(Peticion(TokenPaymentMethods.Crypto), default);

        r.Should().BeOfType<OkObjectResult>();
        billing.VerifyAll();
    }

    [Fact]
    public async Task Si_Billing_cae_la_compra_es_502()
    {
        var billing = new Mock<IBillingClient>();
        billing.Setup(b => b.PurchaseTokensAsync(It.IsAny<PurchaseTokensRequest>(), "jwt.miembro", null, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BillingResult<TokenPurchaseResultDto>.Unavailable("caido"));

        var r = await Controlador(billing).Purchase(Peticion(TokenPaymentMethods.CreditCard), default);

        Objeto(r).StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        Cuerpo<TokenPurchaseResultDto>(r).ErrorCode.Should().Be(TokenPurchaseErrors.BillingUnavailable);
    }

    [Theory]
    [InlineData(StatusCodes.Status409Conflict, TokenPurchaseErrors.IdempotencyKeyReused)]
    [InlineData(StatusCodes.Status400BadRequest, TokenPurchaseErrors.CardPaymentsDisabled)]
    [InlineData(StatusCodes.Status400BadRequest, TokenPurchaseErrors.PaymentServiceUnavailable)]
    public async Task El_estado_y_el_codigo_de_Billing_llegan_tal_cual(int estado, string codigo)
    {
        var billing = new Mock<IBillingClient>();
        billing.Setup(b => b.PurchaseTokensAsync(It.IsAny<PurchaseTokensRequest>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
               .ReturnsAsync(BillingResult<TokenPurchaseResultDto>.Fail(estado, codigo, "mensaje"));

        var r = await Controlador(billing).Purchase(Peticion(TokenPaymentMethods.CreditCard), default);

        Objeto(r).StatusCode.Should().Be(estado);
        Cuerpo<TokenPurchaseResultDto>(r).ErrorCode.Should().Be(codigo);
    }

    [Fact]
    public async Task Bajo_suplantacion_no_se_compra_ni_se_llama_a_Billing()
    {
        var billing = new Mock<IBillingClient>(MockBehavior.Strict);

        var normal = await Controlador(billing, suplantando: true).Purchase(Peticion(TokenPaymentMethods.Crypto), default);
        var saldo  = await Controlador(billing, grant: "permiso", suplantando: true)
            .PurchaseFromBalance(Peticion(TokenPaymentMethods.EWallet), default);
        var cancel = await Controlador(billing, suplantando: true).CancelPurchase("p1", default);

        foreach (var r in new[] { normal, saldo, cancel })
        {
            Objeto(r).StatusCode.Should().Be(StatusCodes.Status403Forbidden);
            Cuerpo<TokenPurchaseResultDto>(r).ErrorCode.Should().Be(TokenPurchaseErrors.Impersonation);
        }
    }

    [Fact]
    public async Task Cancelar_reenvia_el_id_y_el_Bearer()
    {
        var billing = new Mock<IBillingClient>();
        billing.Setup(b => b.CancelTokenPurchaseAsync("p1", "jwt.miembro", It.IsAny<CancellationToken>()))
               .ReturnsAsync(BillingResult<TokenPurchaseResultDto>.Ok(new TokenPurchaseResultDto { PurchaseId = "p1", Status = TokenPurchaseStatuses.Cancelled }));

        var r = await Controlador(billing).CancelPurchase("p1", default);

        r.Should().BeOfType<OkObjectResult>();
        billing.VerifyAll();
    }

    [Fact]
    public async Task El_catalogo_llega_con_CardEnabled_de_Billing()
    {
        var billing = new Mock<IBillingClient>();
        billing.Setup(b => b.GetTokenCatalogAsync("jwt.miembro", It.IsAny<CancellationToken>()))
               .ReturnsAsync(BillingResult<TokenCatalogDto>.Ok(new TokenCatalogDto { CardEnabled = false, MonthlyLimit = 50 }));

        var r = await Controlador(billing).GetPurchasableTypes(default);

        r.Should().BeOfType<OkObjectResult>();
        Cuerpo<TokenCatalogDto>(r).Data!.CardEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Los_fondos_del_wallet_vienen_de_Billing_y_su_caida_es_502()
    {
        var billing = new Mock<IBillingClient>();
        billing.SetupSequence(b => b.GetPurchaseFundsAsync("jwt.miembro", It.IsAny<CancellationToken>()))
               .ReturnsAsync(BillingResult<TokenPurchaseFundsDto>.Ok(new TokenPurchaseFundsDto { CommissionBalance = 5, CardEnabled = true }))
               .ReturnsAsync(BillingResult<TokenPurchaseFundsDto>.Unavailable("caido"));
        var wallet = new WalletController(Mock.Of<IMediator>())
        {
            ControllerContext = new ControllerContext { HttpContext = Contexto() }
        };

        var ok = await wallet.GetPurchaseBalances(billing.Object, default);
        Cuerpo<TokenPurchaseFundsDto>(ok).Data!.CardEnabled.Should().BeTrue();

        var caido = await wallet.GetPurchaseBalances(billing.Object, default);
        Objeto(caido).StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        Cuerpo<TokenPurchaseFundsDto>(caido).ErrorCode.Should().Be(TokenPurchaseErrors.BillingUnavailable);
    }

    // ─── Bearer, resumen y validación ────────────────────────────────────────

    [Theory]
    [InlineData("Bearer abc.def", "abc.def")]
    [InlineData("bearer   abc.def ", "abc.def")]
    [InlineData("Basic xyz", "")]
    [InlineData(null, "")]
    public void BearerToken_saca_el_token_crudo(string? cabecera, string esperado)
    {
        var ctx = new DefaultHttpContext();
        if (cabecera is not null) ctx.Request.Headers.Authorization = cabecera;

        BearerToken.From(ctx.Request).Should().Be(esperado);
    }

    [Fact]
    public async Task El_resumen_cuenta_las_instancias_del_miembro()
    {
        var now = new DateTime(2026, 10, 15, 10, 0, 0);
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        TokenTransaction I(string code, TokenInstanceStatus s, string owner = "AMB-1", DateTime? expira = null) => new()
        {
            MemberId = owner, TokenTypeId = 13, TransactionType = TokenTransactionType.Purchased, Quantity = 1,
            ReferenceId = code, Status = s, ExpiresAt = expira, CreatedBy = "seed", CreationDate = now
        };
        db.TokenTransactions.AddRange(
            I("A1", TokenInstanceStatus.Issued), I("A2", TokenInstanceStatus.Distributed), I("A3", TokenInstanceStatus.Used),
            I("A4", TokenInstanceStatus.Issued, expira: now.AddDays(-1)), I("B1", TokenInstanceStatus.Issued, owner: "AMB-2"));
        db.TokenTransactions.Add(new TokenTransaction
        {
            MemberId = "AMB-1", TokenTypeId = 13, TransactionType = TokenTransactionType.Distributed, Quantity = 2, CreatedBy = "seed", CreationDate = now
        });
        await db.SaveChangesAsync();
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.MemberId).Returns("AMB-1");
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.Now).Returns(now);

        var r = await new GetTokenSummaryHandler(db, user.Object, clock.Object).Handle(new GetTokenSummaryQuery(), default);

        r.Value!.TotalTokens.Should().Be(4);
        r.Value.TotalUsed.Should().Be(1);
        r.Value.TotalAvailable.Should().Be(2);
    }

    [Theory]
    [InlineData(TokenPaymentMethods.CommissionBalance)]
    [InlineData(TokenPaymentMethods.EWallet)]
    [InlineData(TokenPaymentMethods.CreditCard)]
    [InlineData(TokenPaymentMethods.Crypto)]
    public void La_peticion_bien_formada_pasa_la_validacion(string metodo)
        => new PurchaseTokensRequestValidator().TestValidate(Peticion(metodo)).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void La_validacion_rechaza_lo_mal_formado_antes_de_llamar_a_Billing()
    {
        var v = new PurchaseTokensRequestValidator();

        v.TestValidate(new PurchaseTokensRequest { TokenTypeId = 0, Quantity = 1, PaymentMethod = "Crypto", CryptoCurrency = "USDT", IdempotencyKey = "k" })
            .ShouldHaveValidationErrorFor(x => x.TokenTypeId);
        v.TestValidate(new PurchaseTokensRequest { TokenTypeId = 1, Quantity = TokenPurchaseRules.MonthlyLimit + 1, PaymentMethod = "EWallet", IdempotencyKey = "k" })
            .ShouldHaveValidationErrorFor(x => x.Quantity);
        v.TestValidate(new PurchaseTokensRequest { TokenTypeId = 1, Quantity = 1, PaymentMethod = "Paypal", IdempotencyKey = "k" })
            .ShouldHaveValidationErrorFor(x => x.PaymentMethod);
        v.TestValidate(new PurchaseTokensRequest { TokenTypeId = 1, Quantity = 1, PaymentMethod = "EWallet", IdempotencyKey = "con espacio" })
            .ShouldHaveValidationErrorFor(x => x.IdempotencyKey);
        v.TestValidate(new PurchaseTokensRequest { TokenTypeId = 1, Quantity = 1, PaymentMethod = "CreditCard", IdempotencyKey = "k" })
            .ShouldHaveValidationErrorFor(x => x.CreditCardId);
        v.TestValidate(new PurchaseTokensRequest { TokenTypeId = 1, Quantity = 1, PaymentMethod = "Crypto", CryptoCurrency = "DOGE", IdempotencyKey = "k" })
            .ShouldHaveValidationErrorFor(x => x.CryptoCurrency);
    }
}
