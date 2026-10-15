using System.Globalization;
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

    private static Func<HttpRequestMessage, HttpResponseMessage> Gets(
        bool tarjeta = true, string? fondos = null, string? tarjetas = null, string? lista = null, Func<string>? catalogo = null) => r =>
    {
        var path = r.RequestUri!.AbsolutePath;
        if (path.EndsWith("/tokens/types"))         return Json(HttpStatusCode.OK, catalogo?.Invoke() ?? Catalogo(tarjeta));
        if (path.EndsWith("/wallet/balances"))      return Json(HttpStatusCode.OK, fondos ?? Fondos);
        if (path.EndsWith("/tokens/summary"))       return Json(HttpStatusCode.OK, Resumen);
        if (path.EndsWith("/billing/credit-cards")) return Json(HttpStatusCode.OK, tarjetas ?? Tarjetas);
        if (path.EndsWith("/tokens"))               return Json(HttpStatusCode.OK, lista ?? Lista);
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
    public async Task En_cripto_solo_se_ve_la_direccion_que_devuelve_Billing_y_sin_NowPayments()
    {
        // La del catálogo puede haber cambiado cuando se crea la compra: la buena es la del resultado.
        const string DelResultado = "TResultadoDeBillingQwErTy1234567890";
        var page = Montar(r => Task.FromResult(r.Method == HttpMethod.Post
            ? Json(HttpStatusCode.OK, EnCripto.Replace("TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5", DelResultado).Replace("TRC-20", "TRC-20 (nueva)"))
            : Get(r)));

        ElegirVip(page);
        await page.Find("#tokens-tab-Crypto").ClickAsync(new());
        page.WaitForAssertion(() => page.Markup.Should().Contain("Tokens.CryptoAmountDue").And.Contain("Tokens.CryptoCurrency"));
        page.Markup.Should().NotContain("TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5", "antes de pedir no se enseña la dirección del catálogo")
            .And.NotContain("Send your payment", "los textos del selector son los de la pantalla, traducidos")
            .And.NotContain("NowPayments");
        await page.Find("#tokens-buy").ClickAsync(new());

        page.WaitForAssertion(() =>
        {
            page.Markup.Should().Contain("Tokens.PurchaseCryptoPending");
            page.Find("#tokens-crypto-address").TextContent.Should().Be(DelResultado);
            page.Find("#tokens-crypto-network").TextContent.Should().Contain("TRC-20 (nueva)");
        });
        page.Markup.Should().NotContain("TQ5NkEQwK6x9C2iGBDbTj9JxF8tqmL7Ak5");
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

        // Un segundo clic de verdad mientras se envía (el navegador no lo dejaría, pero el manejador
        // tampoco debe mandar otra compra) y la selección bloqueada.
        _ = page.Find("#tokens-buy").ClickAsync(new());
        page.Find("#tokens-tab-Crypto").HasAttribute("disabled").Should().BeTrue();
        page.Find("#tokens-purchase-type").HasAttribute("disabled").Should().BeTrue();
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
    [InlineData(TokenDistributionErrors.RecipientNotInDownline, "Tokens.Error.Recipient")]
    [InlineData(TokenDistributionErrors.InvalidRecipient,      "Tokens.Error.SelfRecipient")]
    [InlineData(TokenPurchaseMessages.InternalErrorCode,       "Tokens.Error.Unavailable")]
    [InlineData(ImpersonationScope.ReadOnlyErrorCode,          "Tokens.Error.Impersonation")]
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
    [InlineData(TokenPurchaseMessages.InternalErrorCode,       HttpStatusCode.InternalServerError, true)]
    [InlineData("ALGO_NUEVO",                                  HttpStatusCode.BadGateway, true)]
    [InlineData(StepUpGrant.CooldownErrorCode,                 HttpStatusCode.Forbidden,  false)]
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

    // ── Distribuir: clave de idempotencia por intento (revisión de la Tarea 14, I3) ──

    /// <summary>
    /// Monta la pantalla con saldo de VIP y un miembro del equipo, y responde a cada POST de distribuir
    /// con lo que diga <paramref name="distribuir"/> (que anota la clave de la cabecera).
    /// </summary>
    private IRenderedComponent<TokensPage> MontarDistribucion(List<string?> claves, Func<int, HttpResponseMessage> distribuir)
    {
        return Montar(r =>
        {
            var path = r.RequestUri!.AbsolutePath;
            if (r.Method == HttpMethod.Post && path.EndsWith("/tokens/distribute"))
            {
                claves.Add(r.Headers.TryGetValues(TokenDistributionErrors.IdempotencyHeader, out var v) ? v.Single() : null);
                return Task.FromResult(distribuir(claves.Count));
            }
            if (path.EndsWith("/tokens/balances"))
                return Task.FromResult(Json(HttpStatusCode.OK, """{"success":true,"data":[{"tokenTypeId":13,"tokenTypeName":"VIP Member","balance":3}]}"""));
            if (path.EndsWith("/team/enrollment/my-team"))
                return Task.FromResult(Json(HttpStatusCode.OK, """{"success":true,"data":{"items":[{"memberId":"AMB-2","fullName":"Ana"}],"totalCount":1}}"""));
            return Task.FromResult(Get(r));
        });
    }

    private static void PrepararDistribucion(IRenderedComponent<TokensPage> page)
    {
        page.WaitForAssertion(() => page.Find("#tokens-distribute-type option[value='13']"));
        page.Find("#tokens-distribute-type").Change("13");
        page.Find("#tokens-member-search").Input("An");
        page.WaitForAssertion(() => page.Find(".tokens-member-option"));
        page.Find(".tokens-member-option").Click();
    }

    private static void Distribuir(IRenderedComponent<TokensPage> page, int intento)
    {
        page.WaitForAssertion(() => page.Find("#tokens-distribute").HasAttribute("disabled").Should().BeFalse());
        page.Find("#tokens-distribute").Click();
    }

    [Fact]
    public void Distribuir_conserva_la_clave_tras_una_caida_y_la_renueva_tras_un_fallo_definitivo()
    {
        var claves = new List<string?>();
        var page = MontarDistribucion(claves, n => n switch
        {
            1 => throw new HttpRequestException("red caída"),
            2 => Error(HttpStatusCode.Conflict, TokenPurchaseErrors.Busy),
            3 => Error(HttpStatusCode.BadRequest, TokenDistributionErrors.RecipientNotInDownline),
            _ => Json(HttpStatusCode.OK, """{"success":true,"data":true}""")
        });
        PrepararDistribucion(page);

        Distribuir(page, 1);
        page.WaitForAssertion(() => page.Find("#tokens-distribute-error").TextContent.Should().Contain("Tokens.Error.DistributionUnavailable"));
        Distribuir(page, 2);
        page.WaitForAssertion(() => page.Find("#tokens-distribute-error").TextContent.Should().Contain("Tokens.Error.DistributionBusy"));
        Distribuir(page, 3);
        page.WaitForAssertion(() => page.Find("#tokens-distribute-error").TextContent.Should().Contain("Tokens.Error.Recipient"));
        Distribuir(page, 4);
        page.WaitForAssertion(() => claves.Should().HaveCount(4));

        claves.Should().OnlyContain(c => TokenPurchaseRules.IsValidIdempotencyKey(c));
        claves[1].Should().Be(claves[0], "tras una caída no se sabe si entró: se repite con la misma clave");
        claves[2].Should().Be(claves[1], "ocupado no movió nada: la misma clave sirve");
        claves[3].Should().NotBe(claves[2], "un fallo definitivo cierra la clave");
    }

    [Fact]
    public void Distribuir_con_exito_renueva_la_clave_y_una_repetida_tiene_su_texto()
    {
        var claves = new List<string?>();
        var page = MontarDistribucion(claves, n => n == 1
            ? Json(HttpStatusCode.OK, """{"success":true,"data":true}""")
            : Error(HttpStatusCode.Conflict, TokenPurchaseErrors.IdempotencyKeyReused));
        PrepararDistribucion(page);

        Distribuir(page, 1);
        page.WaitForAssertion(() => page.Markup.Should().Contain("Tokens.DistributeDone"));
        PrepararDistribucion(page);
        Distribuir(page, 2);

        page.WaitForAssertion(() => page.Find("#tokens-distribute-error").TextContent.Should().Contain("Tokens.Error.DistributionRepeated"));
        claves[1].Should().NotBe(claves[0], "tras un éxito, otra distribución es otra clave");
    }

    [Theory]
    [InlineData(TokenPurchaseErrors.IdempotencyKeyReused,              "Tokens.Error.DistributionRepeated")]
    [InlineData(TokenPurchaseErrors.Busy,                              "Tokens.Error.DistributionBusy")]
    [InlineData(TokenDistributionErrors.IdempotencyUnavailable,        "Tokens.Error.DistributionUnavailable")]
    [InlineData(TokenDistributionErrors.RecipientNotActive,            "Tokens.Error.RecipientNotActive")]
    [InlineData(TokenDistributionErrors.Impersonation,                 "Tokens.Error.ImpersonationDistribute")]
    [InlineData(TokenDistributionErrors.InsufficientTokenBalance,      "Tokens.Error.NotEnoughTokens")]
    [InlineData(TokenDistributionErrors.InvalidRecipient,              "Tokens.Error.SelfRecipient")]
    [InlineData(TokenDistributionErrors.RecipientNotFound,             "Tokens.Error.Recipient")]
    [InlineData(TokenPurchaseMessages.InternalErrorCode,               "Tokens.Error.DistributionRepeated")]
    public void Cada_codigo_de_distribucion_tiene_su_texto(string codigo, string clave) =>
        TokenPurchaseMessages.DistributionErrorKeyFor(codigo).Should().Be(clave);

    [Theory]
    [InlineData(TokenPurchaseErrors.Busy,                       HttpStatusCode.Conflict,            true)]
    [InlineData(TokenDistributionErrors.IdempotencyUnavailable, HttpStatusCode.ServiceUnavailable,  true)]
    [InlineData(null,                                           HttpStatusCode.InternalServerError, true)]
    [InlineData(TokenPurchaseMessages.InternalErrorCode,        HttpStatusCode.InternalServerError, true)]
    [InlineData(TokenPurchaseErrors.IdempotencyKeyReused,       HttpStatusCode.Conflict,            false)]
    [InlineData(TokenDistributionErrors.RecipientNotActive,     HttpStatusCode.BadRequest,          false)]
    [InlineData(TokenDistributionErrors.InsufficientTokenBalance, HttpStatusCode.BadRequest,        false)]
    [InlineData(null,                                           HttpStatusCode.BadRequest,          false)]
    public void La_clave_de_distribucion_solo_se_conserva_si_se_puede_repetir(string? codigo, HttpStatusCode estado, bool conserva) =>
        TokenPurchaseMessages.DistributionKeepsKey(codigo, estado).Should().Be(conserva);

    // ── Revisión de la Tarea 15 ───────────────────────────────────────────────

    [Fact]
    public async Task Un_500_INTERNAL_ERROR_conserva_la_clave_y_se_explica_como_caida()
    {
        var claves = new List<string>();
        var page = Montar(async r =>
        {
            if (r.Method != HttpMethod.Post) return Get(r);
            claves.Add(await ClaveDe(r));
            return claves.Count == 1
                ? Error(HttpStatusCode.InternalServerError, TokenPurchaseMessages.InternalErrorCode)
                : Json(HttpStatusCode.OK, EnCripto);
        });

        ElegirVip(page);
        await page.Find("#tokens-tab-Crypto").ClickAsync(new());
        await page.Find("#tokens-buy").ClickAsync(new());
        page.WaitForAssertion(() => page.Find("#tokens-purchase-error").TextContent.Should().Contain("Tokens.Error.Unavailable"));
        await page.Find("#tokens-buy").ClickAsync(new());

        page.WaitForAssertion(() => claves.Should().HaveCount(2));
        claves[1].Should().Be(claves[0]);
    }

    [Fact]
    public async Task Tras_un_tiempo_agotado_en_el_navegador_se_repite_la_misma_compra()
    {
        var claves = new List<string>();
        var page = Montar(async r =>
        {
            if (r.Method != HttpMethod.Post) return Get(r);
            claves.Add(await ClaveDe(r));
            if (claves.Count == 1) throw new TaskCanceledException("timeout del HttpClient");
            return Json(HttpStatusCode.OK, EnCripto);
        });

        ElegirVip(page);
        await page.Find("#tokens-tab-Crypto").ClickAsync(new());
        await page.Find("#tokens-buy").ClickAsync(new());
        page.WaitForAssertion(() => page.Find("#tokens-purchase-pending"));
        await page.Find("#tokens-buy").ClickAsync(new());

        page.WaitForAssertion(() => claves.Should().HaveCount(2));
        claves[1].Should().Be(claves[0]);
    }

    [Fact]
    public async Task Con_una_compra_sin_resolver_cambiar_la_seleccion_no_cambia_lo_que_se_repite()
    {
        var cuerpos = new List<string>();
        var page = Montar(async r =>
        {
            if (r.Method != HttpMethod.Post) return Get(r);
            cuerpos.Add(await r.Content!.ReadAsStringAsync());
            if (cuerpos.Count == 1) throw new HttpRequestException("caida");
            return Json(HttpStatusCode.OK, EnCripto);
        });

        ElegirVip(page);
        await page.Find("#tokens-tab-Crypto").ClickAsync(new());
        await page.Find("#tokens-buy").ClickAsync(new());
        page.WaitForAssertion(() => page.Find("#tokens-purchase-pending").TextContent.Should().Contain("Tokens.PendingAttempt"));

        // Todo bloqueado; y aunque llegue el evento, no cambia nada.
        page.Find("#tokens-tab-CommissionBalance").HasAttribute("disabled").Should().BeTrue();
        page.Find("#tokens-purchase-qty").HasAttribute("disabled").Should().BeTrue();
        page.Find("#tokens-crypto").HasAttribute("disabled").Should().BeTrue();
        await page.Find("#tokens-tab-CommissionBalance").ClickAsync(new());
        page.Find("#tokens-purchase-qty").Change("5");
        page.Find("#tokens-tab-Crypto").GetAttribute("aria-selected").Should().Be("true");
        page.Find("#tokens-buy").HasAttribute("disabled").Should().BeFalse("repetir la misma compra sí se puede");

        await page.Find("#tokens-buy").ClickAsync(new());

        page.WaitForAssertion(() => cuerpos.Should().HaveCount(2));
        cuerpos[1].Should().Be(cuerpos[0], "se repite exactamente la petición guardada: mismo cuerpo y misma clave");
        page.WaitForAssertion(() =>
        {
            page.FindAll("#tokens-purchase-pending").Should().BeEmpty("la repetición dio un resultado");
            page.Find("#tokens-tab-CommissionBalance").HasAttribute("disabled").Should().BeFalse();
        });
    }

    [Fact]
    public async Task Los_importes_en_dolares_no_dependen_de_la_cultura_de_quien_mira()
    {
        var antes = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");
        try
        {
            var page = Montar(r => Task.FromResult(r.Method == HttpMethod.Post ? Json(HttpStatusCode.OK, EnCripto) : Get(r)));

            ElegirVip(page);
            page.WaitForAssertion(() => page.Find("#tokens-commission-balance").TextContent.Should().Be("$500.00"));
            page.Find("#tokens-unit-price").TextContent.Should().Contain("|$99.99");
            page.Find("#tokens-purchase-qty").Change("2");
            page.Find("#tokens-total").TextContent.Should().Contain("|$199.98");

            await page.Find("#tokens-tab-Crypto").ClickAsync(new());
            page.Find("#tokens-crypto-due").TextContent.Should().Contain("|$199.98|USDT");
            page.Find("#tokens-purchase-qty").Change("1");
            await page.Find("#tokens-buy").ClickAsync(new());

            page.WaitForAssertion(() => page.Markup.Should().Contain("Tokens.PurchaseCryptoPending|TP2|$99.99|USDT"));
            page.Markup.Should().NotContain("R$");
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = antes;
        }
    }

    [Fact]
    public void Sin_cuenta_eWallet_no_se_puede_pagar_con_ella_y_se_dice()
    {
        var page = Montar(r => Task.FromResult(Get(r)));

        ElegirVip(page);
        page.Find("#tokens-tab-EWallet").Click();

        page.WaitForAssertion(() => page.Find("#tokens-no-ewallet").TextContent.Should().Contain("Tokens.Error.NoEWallet"));
        page.Find("#tokens-buy").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Al_recargar_el_catalogo_la_cantidad_no_pasa_del_cupo_que_queda()
    {
        var catalogos = 0;
        var page = Montar(r => Task.FromResult(r.Method == HttpMethod.Post
            ? Json(HttpStatusCode.OK, EnCripto)
            : Gets(catalogo: () => Interlocked.Increment(ref catalogos) == 1
                ? Catalogo()
                : Catalogo().Replace("\"monthlyRemaining\":50", "\"monthlyRemaining\":3"))(r)));

        ElegirVip(page);
        page.Find("#tokens-purchase-qty").Change("10");
        await page.Find("#tokens-tab-Crypto").ClickAsync(new());
        await page.Find("#tokens-buy").ClickAsync(new());

        page.WaitForAssertion(() =>
        {
            page.Find("#tokens-remaining").TextContent.Should().Contain("|3");
            page.FindAll("#tokens-purchase-qty option").Should().HaveCount(3);
            page.Find("#tokens-total").TextContent.Should().Contain("|$299.97");
        });
        page.Find("#tokens-buy").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task El_aviso_de_carga_fallida_se_apaga_cuando_la_carga_vuelve_a_ir_bien()
    {
        var resumenes = 0;
        var page = Montar(r =>
        {
            if (r.Method == HttpMethod.Post) return Task.FromResult(Json(HttpStatusCode.OK, EnCripto));
            if (r.RequestUri!.AbsolutePath.EndsWith("/tokens/summary") && Interlocked.Increment(ref resumenes) == 1)
                return Task.FromResult(Json(HttpStatusCode.InternalServerError, "{}"));
            return Task.FromResult(Get(r));
        });

        page.WaitForAssertion(() => page.Find("#tokens-load-error"));
        ElegirVip(page);
        await page.Find("#tokens-tab-Crypto").ClickAsync(new());
        await page.Find("#tokens-buy").ClickAsync(new());

        page.WaitForAssertion(() => page.FindAll("#tokens-load-error").Should().BeEmpty());
    }

    [Fact]
    public async Task Pestanas_tarjetas_y_copiar_son_accesibles()
    {
        const string ConVencida = """{"success":true,"data":[{"id":"c1","last4":"1111","cardBrand":"Visa","expiryMonth":12,"expiryYear":2030,"isExpired":false},{"id":"c2","last4":"2222","cardBrand":"Visa","expiryMonth":1,"expiryYear":2020,"isExpired":true}]}""";
        const string ConCodigo  = """{"success":true,"data":{"items":[{"tokenTypeName":"VIP","transactionType":"Purchase","tokenCode":"ABC1234","isUsed":false,"occurredAt":"2026-10-15T10:00:00"}],"totalCount":1}}""";
        var page = Montar(r => Task.FromResult(Gets(tarjetas: ConVencida, lista: ConCodigo)(r)));

        ElegirVip(page);
        page.Find("[role=tablist]").GetAttribute("aria-label").Should().Be("Tokens.PaymentMethodsAria");
        page.Find("#tokens-tab-CommissionBalance").GetAttribute("role").Should().Be("tab");
        page.Find("#tokens-tab-CommissionBalance").GetAttribute("aria-selected").Should().Be("true");
        page.Find("#tokens-tab-Crypto").GetAttribute("aria-selected").Should().Be("false");
        page.Find("[role=tabpanel]").Id.Should().Be("tokens-pay-panel");

        await page.Find("#tokens-tab-CreditCard").ClickAsync(new());
        page.WaitForAssertion(() => page.Find("#tokens-card-c1"));
        page.Find("#tokens-card-c1").GetAttribute("aria-pressed").Should().Be("false");
        await page.Find("#tokens-card-c1").ClickAsync(new());
        page.Find("#tokens-card-c1").GetAttribute("aria-pressed").Should().Be("true");
        page.Find("#tokens-card-c2 .tokens-card-expired").TextContent.Should().Be("Tokens.CardExpired");

        page.WaitForAssertion(() => page.Find(".tokens-copy").GetAttribute("aria-label").Should().Be("Tokens.CopyCode|ABC1234"));
    }

    [Fact]
    public void Mientras_se_envia_el_spinner_es_decorativo_y_hay_texto_oculto()
    {
        var enVuelo = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = Montar(r => r.Method != HttpMethod.Post ? Task.FromResult(Get(r)) : enVuelo.Task);

        ElegirVip(page);
        page.Find("#tokens-tab-Crypto").Click();
        _ = page.Find("#tokens-buy").ClickAsync(new());

        page.WaitForAssertion(() =>
        {
            page.Find("#tokens-buy .spinner-border").GetAttribute("aria-hidden").Should().Be("true");
            page.Find("#tokens-buy .visually-hidden").TextContent.Should().Be("Common.Loading");
        });
        enVuelo.SetResult(Json(HttpStatusCode.OK, EnCripto));
    }

    [Theory]
    [InlineData("MLMConquerorGlobalEdition.BizCenterWeb/Components/Pages/Tokens.razor")]
    [InlineData("MLMConquerorGlobalEdition.BizCenterApp/Components/Pages/Tokens.razor")]
    public void Los_anfitriones_del_miembro_apagan_compra_y_distribucion_al_suplantar(string ruta)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx"))) dir = dir.Parent;
        dir.Should().NotBeNull("las pruebas corren dentro del repositorio");

        var pagina = File.ReadAllText(Path.Combine(dir!.FullName, ruta));

        pagina.Should().Contain("PurchaseEnabled=\"@(!ViewContext.IsImpersonating)\"")
            .And.Contain("DistributionEnabled=\"@(!ViewContext.IsImpersonating)\"");
    }

    [Fact]
    public void Distribuir_a_uno_mismo_y_un_500_tienen_texto_veraz()
    {
        var claves = new List<string?>();
        var page = MontarDistribucion(claves, n => n == 1
            ? Error(HttpStatusCode.BadRequest, TokenDistributionErrors.InvalidRecipient)
            : Error(HttpStatusCode.InternalServerError, TokenPurchaseMessages.InternalErrorCode));
        PrepararDistribucion(page);

        Distribuir(page, 1);
        page.WaitForAssertion(() => page.Find("#tokens-distribute-error").TextContent.Should().Contain("Tokens.Error.SelfRecipient"));
        Distribuir(page, 2);
        page.WaitForAssertion(() => page.Find("#tokens-distribute-error").TextContent.Should().Contain("Tokens.Error.DistributionRepeated"));
        Distribuir(page, 3);

        page.WaitForAssertion(() => claves.Should().HaveCount(3));
        claves[1].Should().NotBe(claves[0], "enviarse a uno mismo es un fallo definitivo");
        claves[2].Should().Be(claves[1], "tras un 500 no se sabe si se hizo: se repite con la misma clave y el BizCenter la reconoce");
    }
}
