using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp;
using MLMConquerorGlobalEdition.SharedComponents.Components.Tokens;
using MLMConquerorGlobalEdition.SharedComponents.Extensions;
using MLMConquerorGlobalEdition.SharedComponents.Resources;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// La pantalla de tokens tragaba los errores, llamaba a endpoints inexistentes y mandaba el
/// TokenTypeId como texto. Estas pruebas fijan el contrato real, los errores traducidos, la clave de
/// idempotencia que sobrevive a una caída (y la que no debe sobrevivir), el step-up al pagar con saldo
/// (y no con tarjeta), la pestaña de tarjeta solo si Billing la acepta y la dirección de depósito al
/// pagar en cripto, con la opción de cancelar.
/// </summary>
public class PantallaDeTokensTests : BunitContext
{
    private sealed class Manejador(Func<HttpRequestMessage, Task<HttpResponseMessage>> f) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) => f(r);
    }

    private sealed class ClienteQueConcede : IStepUpClient
    {
        public int Permisos { get; private set; }

        public Task<StepUpOutcome<StepUpChallenge>> RequestChallengeAsync(string o, CancellationToken ct = default) =>
            Task.FromResult(new StepUpOutcome<StepUpChallenge>(true, null,
                new StepUpChallenge("reto", "Email", "n***@x.com", DateTime.Now.AddMinutes(5))));

        public Task<StepUpOutcome<string>> VerifyAsync(string o, string c, string code, CancellationToken ct = default) =>
            Task.FromResult(new StepUpOutcome<string>(true, null, "permiso-" + ++Permisos));
    }

    private readonly ClienteQueConcede _stepUp = new();

    private static HttpResponseMessage Json(HttpStatusCode s, string json) =>
        new(s) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string Catalogo(bool tarjeta = true) => """{"success":true,"data":{"monthlyLimit":50,"purchasedThisMonth":0,"monthlyRemaining":50,"items":[{"tokenTypeId":13,"name":"VIP Member","unitPrice":99.99,"purchasable":true},{"tokenTypeId":92,"name":"VIP Member FREE","purchasable":false,"notPurchasableReason":"TOKEN_TYPE_NOT_FOR_SALE"}],"cryptoDeposits":[{"currency":"USDT","address":"TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5","network":"TRC-20"}],"cardEnabled":"""
        + (tarjeta ? "true" : "false") + "}}";

    private const string Fondos   = """{"success":true,"data":{"commissionBalance":500,"eWalletBalance":null,"hasEWallet":false,"cardEnabled":true}}""";
    private const string Resumen  = """{"success":true,"data":{"totalTokens":0,"totalUsed":0,"totalAvailable":0}}""";
    private const string Lista    = """{"success":true,"data":{"items":[],"totalCount":0}}""";
    private const string Vacio    = """{"success":true,"data":[]}""";
    private const string Tarjetas = """{"success":true,"data":[{"id":"c1","last4":"1111","cardBrand":"Visa","expiryMonth":12,"expiryYear":2030,"isExpired":false}]}""";
    private const string Hecha    = """{"success":true,"data":{"purchaseId":"p1","orderId":"o1","orderNo":"TP1","status":"Completed","quantity":1,"totalAmount":99.99,"tokenCodes":["ABC1234"],"monthlyRemaining":49}}""";
    private const string IdCripto = "6f1c1f0e-2b7a-4c55-9a2e-3b0f2c1d4e5f";
    private const string EnCripto = """{"success":true,"data":{"purchaseId":"6f1c1f0e-2b7a-4c55-9a2e-3b0f2c1d4e5f","orderId":"o2","orderNo":"TP2","status":"AwaitingCryptoConfirmation","quantity":1,"totalAmount":99.99,"tokenCodes":[],"monthlyRemaining":49,"cryptoCurrency":"USDT","cryptoDepositAddress":"TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5","cryptoNetwork":"TRC-20"}}""";
    private const string Cancelada = """{"success":true,"data":{"purchaseId":"6f1c1f0e-2b7a-4c55-9a2e-3b0f2c1d4e5f","orderId":"o2","status":"Cancelled","quantity":1,"totalAmount":99.99,"tokenCodes":[],"monthlyRemaining":50}}""";

    private static HttpResponseMessage Error(HttpStatusCode s, string code) =>
        Json(s, $$"""{"success":false,"errorCode":"{{code}}","data":null}""");

    private static Func<HttpRequestMessage, HttpResponseMessage> Gets(bool tarjeta = true) => r =>
    {
        var path = r.RequestUri!.AbsolutePath;
        if (path.EndsWith("/tokens/types"))         return Json(HttpStatusCode.OK, Catalogo(tarjeta));
        if (path.EndsWith("/wallet/balances"))      return Json(HttpStatusCode.OK, Fondos);
        if (path.EndsWith("/tokens/summary"))       return Json(HttpStatusCode.OK, Resumen);
        if (path.EndsWith("/billing/credit-cards")) return Json(HttpStatusCode.OK, Tarjetas);
        if (path.EndsWith("/tokens"))               return Json(HttpStatusCode.OK, Lista);
        return Json(HttpStatusCode.OK, Vacio); // tokens/balances
    };

    private static HttpResponseMessage Get(HttpRequestMessage r) => Gets()(r);

    private static string? Permiso(HttpRequestMessage r) =>
        r.Headers.TryGetValues(StepUpGrant.HeaderName, out var v) ? v.Single() : null;

    private static async Task<string> ClaveDe(HttpRequestMessage r)
    {
        using var doc = JsonDocument.Parse(await r.Content!.ReadAsStringAsync());
        return doc.RootElement.GetProperty("idempotencyKey").GetString()!;
    }

    private IRenderedComponent<TokensPage> Montar(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> f, bool compra = true)
    {
        Services.AddSharedComponents();
        Services.AddSingleton(new HttpClient(new Manejador(f)) { BaseAddress = new Uri("https://api.pruebas/") });
        Services.AddSingleton<IStringLocalizer<SharedResources>>(new StepUpDialogTests.LocalizadorDeClaves());
        Services.AddSingleton<IStepUpClient>(_stepUp);
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<TokensPage>(p => p
            .Add(c => c.MemberProfileId, "AMB-1")
            .Add(c => c.PurchaseEnabled, compra)
            .Add(c => c.DistributionEnabled, compra));
    }

    private static void ElegirVip(IRenderedComponent<TokensPage> page)
    {
        page.WaitForAssertion(() => page.Find("#tokens-purchase-type option[value='13']"));
        page.Find("#tokens-purchase-type").Change("13");
    }

    /// <summary>Confirma el código del diálogo de step-up que la compra con saldo acaba de abrir.</summary>
    private static void ConfirmarCodigo(IRenderedComponent<TokensPage> page)
    {
        page.WaitForAssertion(() => page.Find(".stepup-target"));
        page.Find("#stepup-code").Input("123456");
        _ = page.Find(".stepup-confirm").ClickAsync(new());
    }

    [Fact]
    public async Task Un_error_de_compra_se_ve_traducido()
    {
        var page = Montar(r => Task.FromResult(r.Method == HttpMethod.Post
            ? Error(HttpStatusCode.BadRequest, TokenPurchaseErrors.MonthlyLimitReached)
            : Get(r)));

        ElegirVip(page);
        await page.Find("#tokens-buy").ClickAsync(new());

        page.WaitForAssertion(() => page.Find("#tokens-purchase-error").TextContent.Should().Contain("Tokens.Error.MonthlyLimit"));
    }

    [Fact]
    public async Task Manda_el_contrato_del_backend_sin_memberId_y_muestra_los_codigos()
    {
        string? cuerpo = null;
        var page = Montar(async r =>
        {
            if (r.Method != HttpMethod.Post) return Get(r);
            cuerpo = await r.Content!.ReadAsStringAsync();
            return Json(HttpStatusCode.OK, Hecha);
        });

        ElegirVip(page);
        await page.Find("#tokens-buy").ClickAsync(new());

        page.WaitForAssertion(() => page.Markup.Should().Contain("Tokens.PurchaseDone").And.Contain("ABC1234"));
        using var json = JsonDocument.Parse(cuerpo!);
        json.RootElement.GetProperty("tokenTypeId").GetInt32().Should().Be(13);
        json.RootElement.GetProperty("paymentMethod").GetString().Should().Be(TokenPaymentMethods.CommissionBalance);
        json.RootElement.GetProperty("idempotencyKey").GetString().Should().NotBeNullOrWhiteSpace();
        json.RootElement.TryGetProperty("memberId", out _).Should().BeFalse();
    }

    [Fact]
    public void Muestra_el_cupo_del_mes()
    {
        var page = Montar(r => Task.FromResult(Get(r)));

        page.WaitForAssertion(() => page.Find("#tokens-remaining").TextContent.Should().Contain("Tokens.MonthlyRemaining|50"));
    }

    [Fact]
    public async Task Tras_una_caida_el_reintento_reusa_la_clave()
    {
        var claves = new List<string>();
        var page = Montar(async r =>
        {
            if (r.Method != HttpMethod.Post) return Get(r);
            claves.Add(await ClaveDe(r));
            if (claves.Count == 1) throw new HttpRequestException("caida");
            return Json(HttpStatusCode.OK, Hecha);
        });

        ElegirVip(page);
        await page.Find("#tokens-buy").ClickAsync(new());
        page.WaitForAssertion(() => page.Find("#tokens-purchase-error").TextContent.Should().Contain("Tokens.Error.Unavailable"));
        await page.Find("#tokens-buy").ClickAsync(new());

        page.WaitForAssertion(() => page.Markup.Should().Contain("Tokens.PurchaseDone"));
        claves.Should().HaveCount(2);
        claves[1].Should().Be(claves[0]);
    }

    [Fact]
    public void Pagar_con_saldo_pide_el_codigo_y_reintenta_con_el_permiso()
    {
        var posts = new List<(string Path, string? Grant)>();
        var page = Montar(r =>
        {
            if (r.Method != HttpMethod.Post) return Task.FromResult(Get(r));
            var grant = Permiso(r);
            posts.Add((r.RequestUri!.AbsolutePath, grant));
            return Task.FromResult(grant is null
                ? Error(HttpStatusCode.Forbidden, StepUpGrant.RequiredErrorCode)
                : Json(HttpStatusCode.OK, Hecha));
        });

        ElegirVip(page);
        _ = page.Find("#tokens-buy").ClickAsync(new());          // queda esperando el código
        ConfirmarCodigo(page);

        page.WaitForAssertion(() => page.Markup.Should().Contain("Tokens.PurchaseDone"));
        posts.Should().HaveCount(2).And.OnlyContain(p => p.Path.EndsWith("/tokens/purchase/balance"));
        posts[1].Grant.Should().Be("permiso-1");
    }

    [Fact]
    public void Tras_BILLING_UNAVAILABLE_con_saldo_pide_un_codigo_nuevo_y_repite_la_misma_clave()
    {
        var conPermiso = new List<(string Grant, string Clave)>();
        var page = Montar(async r =>
        {
            if (r.Method != HttpMethod.Post) return Get(r);
            var grant = Permiso(r);
            if (grant is null) return Error(HttpStatusCode.Forbidden, StepUpGrant.RequiredErrorCode);
            conPermiso.Add((grant, await ClaveDe(r)));
            return conPermiso.Count == 1
                ? Error(HttpStatusCode.BadGateway, TokenPurchaseErrors.BillingUnavailable)
                : Json(HttpStatusCode.OK, Hecha);
        });

        ElegirVip(page);
        _ = page.Find("#tokens-buy").ClickAsync(new());
        ConfirmarCodigo(page);
        page.WaitForAssertion(() => page.Find("#tokens-purchase-error").TextContent.Should().Contain("Tokens.Error.Unavailable"));

        _ = page.Find("#tokens-buy").ClickAsync(new());
        ConfirmarCodigo(page);

        page.WaitForAssertion(() => page.Markup.Should().Contain("Tokens.PurchaseDone"));
        conPermiso.Should().HaveCount(2);
        conPermiso[1].Grant.Should().NotBe(conPermiso[0].Grant, "el permiso anterior ya se quemó");
        conPermiso[1].Clave.Should().Be(conPermiso[0].Clave, "si la compra entró, Billing devuelve la repetición");
    }

    [Fact]
    public void STEP_UP_INVALID_tras_el_codigo_conserva_la_clave_y_pide_repetir()
    {
        var conPermiso = new List<string>();
        var page = Montar(async r =>
        {
            if (r.Method != HttpMethod.Post) return Get(r);
            if (Permiso(r) is null) return Error(HttpStatusCode.Forbidden, StepUpGrant.RequiredErrorCode);
            conPermiso.Add(await ClaveDe(r));
            return conPermiso.Count == 1
                ? Error(HttpStatusCode.Forbidden, StepUpGrant.InvalidErrorCode)
                : Json(HttpStatusCode.OK, Hecha);
        });

        ElegirVip(page);
        _ = page.Find("#tokens-buy").ClickAsync(new());
        ConfirmarCodigo(page);
        page.WaitForAssertion(() => page.Find("#tokens-purchase-error").TextContent.Should().Contain("StepUp.RetryFailed"));

        _ = page.Find("#tokens-buy").ClickAsync(new());
        ConfirmarCodigo(page);

        page.WaitForAssertion(() => page.Markup.Should().Contain("Tokens.PurchaseDone"));
        conPermiso.Should().HaveCount(2);
        conPermiso[1].Should().Be(conPermiso[0]);
    }

    [Theory]
    [InlineData(TokenPurchaseErrors.BillingContractError)]
    [InlineData(TokenPurchaseErrors.BillingAuthMismatch)]
    public async Task Un_fallo_de_configuracion_entre_servicios_no_conserva_la_clave(string codigo)
    {
        var claves = new List<string>();
        var page = Montar(async r =>
        {
            if (r.Method != HttpMethod.Post) return Get(r);
            claves.Add(await ClaveDe(r));
            return Error(HttpStatusCode.BadGateway, codigo);
        });

        ElegirVip(page);
        await page.Find("#tokens-tab-Crypto").ClickAsync(new());
        await page.Find("#tokens-buy").ClickAsync(new());
        page.WaitForAssertion(() => page.Find("#tokens-purchase-error").TextContent.Should().Contain("Tokens.Error.ContactSupport"));
        await page.Find("#tokens-buy").ClickAsync(new());

        page.WaitForAssertion(() => claves.Should().HaveCount(2));
        claves[1].Should().NotBe(claves[0]);
    }

    [Fact]
    public async Task Con_tarjeta_va_a_la_ruta_sin_step_up()
    {
        var rutas = new List<string>();
        var page = Montar(r =>
        {
            if (r.Method != HttpMethod.Post) return Task.FromResult(Get(r));
            rutas.Add(r.RequestUri!.AbsolutePath);
            return Task.FromResult(Json(HttpStatusCode.OK, Hecha));
        });

        ElegirVip(page);
        await page.Find("#tokens-tab-CreditCard").ClickAsync(new());
        page.WaitForAssertion(() => page.Find("#tokens-card-c1"));
        await page.Find("#tokens-card-c1").ClickAsync(new());
        await page.Find("#tokens-buy").ClickAsync(new());

        page.WaitForAssertion(() => rutas.Should().ContainSingle().Which.Should().EndWith("/tokens/purchase"));
    }

    [Fact]
    public void Sin_tarjeta_en_Billing_no_hay_pestana_de_tarjeta()
    {
        var page = Montar(r => Task.FromResult(Gets(tarjeta: false)(r)));

        page.WaitForAssertion(() => page.Find("#tokens-purchase-type option[value='13']")); // catálogo cargado
        page.Find("#tokens-tab-Crypto");
        page.FindAll("#tokens-tab-CreditCard").Should().BeEmpty();
    }

    [Fact]
    public void Con_tarjeta_en_Billing_hay_pestana_de_tarjeta()
    {
        var page = Montar(r => Task.FromResult(Gets(tarjeta: true)(r)));

        page.WaitForAssertion(() => page.Find("#tokens-tab-CreditCard"));
    }

    [Fact]
    public async Task En_cripto_muestra_la_direccion_antes_y_despues_de_pedir_y_sin_NowPayments()
    {
        var page = Montar(r => Task.FromResult(r.Method == HttpMethod.Post ? Json(HttpStatusCode.OK, EnCripto) : Get(r)));

        ElegirVip(page);
        await page.Find("#tokens-tab-Crypto").ClickAsync(new());
        page.WaitForAssertion(() => page.Markup.Should().Contain("TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5").And.Contain("Tokens.CryptoAmountDue"));
        page.Markup.Should().NotContain("NowPayments");
        await page.Find("#tokens-buy").ClickAsync(new());

        page.WaitForAssertion(() =>
        {
            page.Markup.Should().Contain("Tokens.PurchaseCryptoPending");
            page.Find("#tokens-crypto-address").TextContent.Should().Be("TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5");
            page.Find("#tokens-crypto-network").TextContent.Should().Contain("TRC-20");
        });
    }

    [Fact]
    public async Task Se_puede_cancelar_la_compra_en_cripto_propia()
    {
        var posts = new List<string>();
        var page = Montar(r =>
        {
            if (r.Method != HttpMethod.Post) return Task.FromResult(Get(r));
            posts.Add(r.RequestUri!.AbsolutePath);
            return Task.FromResult(r.RequestUri.AbsolutePath.EndsWith("/cancel")
                ? Json(HttpStatusCode.OK, Cancelada)
                : Json(HttpStatusCode.OK, EnCripto));
        });

        ElegirVip(page);
        await page.Find("#tokens-tab-Crypto").ClickAsync(new());
        await page.Find("#tokens-buy").ClickAsync(new());
        page.WaitForAssertion(() => page.Find("#tokens-crypto-cancel"));
        await page.Find("#tokens-crypto-cancel").ClickAsync(new());

        page.WaitForAssertion(() => page.Markup.Should().Contain("Tokens.Status.Cancelled"));
        posts.Should().HaveCount(2);
        posts[1].Should().EndWith($"/tokens/purchases/{IdCripto}/cancel");
        page.FindAll("#tokens-crypto-cancel").Should().BeEmpty();
    }

    [Fact]
    public async Task Cancelar_algo_que_ya_no_espera_la_transferencia_se_explica()
    {
        var page = Montar(r =>
        {
            if (r.Method != HttpMethod.Post) return Task.FromResult(Get(r));
            return Task.FromResult(r.RequestUri!.AbsolutePath.EndsWith("/cancel")
                ? Error(HttpStatusCode.Conflict, TokenPurchaseErrors.NotAwaitingCrypto)
                : Json(HttpStatusCode.OK, EnCripto));
        });

        ElegirVip(page);
        await page.Find("#tokens-tab-Crypto").ClickAsync(new());
        await page.Find("#tokens-buy").ClickAsync(new());
        page.WaitForAssertion(() => page.Find("#tokens-crypto-cancel"));
        await page.Find("#tokens-crypto-cancel").ClickAsync(new());

        page.WaitForAssertion(() => page.Find("#tokens-purchase-error").TextContent.Should().Contain("Tokens.Error.NotAwaitingCrypto"));
    }

    [Fact]
    public void Mientras_se_envia_el_boton_de_comprar_esta_deshabilitado()
    {
        var enVuelo = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var posts = 0;
        var page = Montar(r =>
        {
            if (r.Method != HttpMethod.Post) return Task.FromResult(Get(r));
            Interlocked.Increment(ref posts);
            return enVuelo.Task;
        });

        ElegirVip(page);
        _ = page.Find("#tokens-buy").ClickAsync(new());

        page.WaitForAssertion(() => page.Find("#tokens-buy").HasAttribute("disabled").Should().BeTrue());
        posts.Should().Be(1);

        enVuelo.SetResult(Json(HttpStatusCode.OK, Hecha));
        page.WaitForAssertion(() => page.Markup.Should().Contain("Tokens.PurchaseDone"));
    }

    [Fact]
    public void Los_tipos_que_no_se_venden_no_se_ofrecen()
    {
        var page = Montar(r => Task.FromResult(Get(r)));

        page.WaitForAssertion(() => page.Find("#tokens-purchase-type option[value='13']"));
        page.FindAll("#tokens-purchase-type option[value='92']").Should().BeEmpty();
    }

    [Fact]
    public void Sin_PurchaseEnabled_no_hay_compra_ni_distribucion_ni_se_pide_el_catalogo()
    {
        var pedidas = new List<string>();
        var page = Montar(r => { lock (pedidas) pedidas.Add(r.RequestUri!.AbsolutePath); return Task.FromResult(Get(r)); }, compra: false);

        page.WaitForAssertion(() => pedidas.Should().Contain(p => p.EndsWith("/tokens/summary")));
        page.FindAll("#tokens-buy").Should().BeEmpty();
        page.FindAll("#tokens-distribute").Should().BeEmpty();
        pedidas.Should().NotContain(p => p.EndsWith("/tokens/types"));
    }

    [Fact]
    public void Si_una_carga_falla_se_avisa_en_vez_de_callar()
    {
        var page = Montar(r => Task.FromResult(r.RequestUri!.AbsolutePath.EndsWith("/tokens/summary")
            ? Json(HttpStatusCode.InternalServerError, "{}")
            : Get(r)));

        page.WaitForAssertion(() => page.Find("#tokens-load-error"));
    }

    [Fact]
    public void Distribuir_suplantando_muestra_el_texto_de_distribucion()
    {
        var page = Montar(r =>
        {
            var path = r.RequestUri!.AbsolutePath;
            if (r.Method == HttpMethod.Post && path.EndsWith("/tokens/distribute"))
                return Task.FromResult(Error(HttpStatusCode.Forbidden, TokenDistributionErrors.Impersonation));
            if (path.EndsWith("/tokens/balances"))
                return Task.FromResult(Json(HttpStatusCode.OK, """{"success":true,"data":[{"tokenTypeId":13,"tokenTypeName":"VIP Member","balance":3}]}"""));
            if (path.EndsWith("/team/enrollment/my-team"))
                return Task.FromResult(Json(HttpStatusCode.OK, """{"success":true,"data":{"items":[{"memberId":"AMB-2","fullName":"Ana"}],"totalCount":1}}"""));
            return Task.FromResult(Get(r));
        });

        page.WaitForAssertion(() => page.Find("#tokens-distribute-type option[value='13']"));
        page.Find("#tokens-distribute-type").Change("13");
        page.Find("#tokens-member-search").Input("An");
        page.WaitForAssertion(() => page.Find(".tokens-member-option"));
        page.Find(".tokens-member-option").Click();
        page.Find("#tokens-distribute").Click();

        page.WaitForAssertion(() => page.Find("#tokens-distribute-error").TextContent.Should().Contain("Tokens.Error.ImpersonationDistribute"));
    }

    // ── Traducción de códigos y vida de la clave ─────────────────────────────

    [Theory]
    [InlineData(TokenPurchaseErrors.MonthlyLimitReached,       "Tokens.Error.MonthlyLimit")]
    [InlineData(TokenPurchaseErrors.Impersonation,             "Tokens.Error.Impersonation")]
    [InlineData(TokenDistributionErrors.Impersonation,         "Tokens.Error.ImpersonationDistribute")]
    [InlineData(TokenPurchaseErrors.CardPaymentsDisabled,      "Tokens.Error.CardDisabled")]
    [InlineData(TokenPurchaseErrors.IdempotencyKeyReused,      "Tokens.Error.Generic")]
    [InlineData(TokenPurchaseErrors.PaymentServiceUnavailable, "Tokens.Error.Unavailable")]
    [InlineData(TokenPurchaseErrors.BillingUnavailable,        "Tokens.Error.Unavailable")]
    [InlineData(TokenPurchaseErrors.BillingContractError,      "Tokens.Error.ContactSupport")]
    [InlineData(TokenPurchaseErrors.BillingAuthMismatch,       "Tokens.Error.ContactSupport")]
    [InlineData(TokenPurchaseErrors.NotAwaitingCrypto,         "Tokens.Error.NotAwaitingCrypto")]
    [InlineData(TokenPurchaseErrors.Busy,                      "Tokens.Error.InProgress")]
    [InlineData(StepUpGrant.InvalidErrorCode,                  "StepUp.RetryFailed")]
    [InlineData(StepUpGrant.RequiredErrorCode,                 "StepUp.RetryFailed")]
    [InlineData("RECIPIENT_NOT_IN_DOWNLINE",                   "Tokens.Error.Recipient")]
    [InlineData("ALGO_NUEVO",                                  "Tokens.Error.Generic")]
    [InlineData(null,                                          "Tokens.Error.Generic")]
    public void Cada_codigo_tiene_su_texto(string? codigo, string clave) =>
        TokenPurchaseMessages.ErrorKeyFor(codigo).Should().Be(clave);

    [Theory]
    [InlineData(TokenPurchaseErrors.BillingUnavailable,        HttpStatusCode.BadGateway, true)]
    [InlineData(TokenPurchaseErrors.InProgress,                HttpStatusCode.Conflict,   true)]
    [InlineData(TokenPurchaseErrors.Busy,                      HttpStatusCode.Conflict,   true)]
    [InlineData(TokenPurchaseErrors.NeedsReview,               HttpStatusCode.Accepted,   true)]
    [InlineData(StepUpGrant.InvalidErrorCode,                  HttpStatusCode.Forbidden,  true)]
    [InlineData(StepUpGrant.RequiredErrorCode,                 HttpStatusCode.Forbidden,  true)]
    [InlineData(null,                                          HttpStatusCode.GatewayTimeout, true)]
    [InlineData(TokenPurchaseErrors.BillingContractError,      HttpStatusCode.BadGateway, false)]
    [InlineData(TokenPurchaseErrors.BillingAuthMismatch,       HttpStatusCode.BadGateway, false)]
    [InlineData(TokenPurchaseErrors.IdempotencyKeyReused,      HttpStatusCode.Conflict,   false)]
    [InlineData(TokenPurchaseErrors.CardPaymentsDisabled,      HttpStatusCode.BadRequest, false)]
    [InlineData(TokenPurchaseErrors.PaymentServiceUnavailable, HttpStatusCode.ServiceUnavailable, false)]
    [InlineData(TokenPurchaseErrors.MonthlyLimitReached,       HttpStatusCode.BadRequest, false)]
    [InlineData(null,                                          HttpStatusCode.BadRequest, false)]
    public void La_clave_solo_se_conserva_si_no_se_sabe_si_entro(string? codigo, HttpStatusCode estado, bool conserva) =>
        TokenPurchaseMessages.KeepsKey(codigo, estado).Should().Be(conserva);

    [Theory]
    [InlineData(TokenPurchaseStatuses.Cancelled, "Tokens.Status.Cancelled")]
    [InlineData(TokenPurchaseStatuses.Rejected,  "Tokens.Status.Rejected")]
    [InlineData(TokenPurchaseStatuses.Expired,   "Tokens.Status.Expired")]
    [InlineData(TokenPurchaseStatuses.Completed, null)]
    public void Los_estados_de_una_cripto_sin_pagar_tienen_texto(string estado, string? clave) =>
        TokenPurchaseMessages.StatusKeyFor(estado).Should().Be(clave);

    [Fact]
    public void El_dialogo_explica_por_que_pide_el_codigo_segun_la_operacion()
    {
        StepUpMessages.WhyKeyFor(StepUpOperations.TokenPurchaseFromBalance).Should().Be("StepUp.WhyTokenPurchase");
        StepUpMessages.WhyKeyFor(StepUpOperations.PayoutMethodChange).Should().Be("StepUp.Why");
    }
}
