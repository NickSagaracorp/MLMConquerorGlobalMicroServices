using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp;
using MLMConquerorGlobalEdition.SharedComponents.Components.Profile;
using MLMConquerorGlobalEdition.SharedComponents.Extensions;
using MLMConquerorGlobalEdition.SharedComponents.Resources;
using MLMConquerorGlobalEdition.SharedComponents.Services;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.CuotaAnual;

/// <summary>
/// La tarjeta de la cuota (Plan K, K21): enseña el estado; en el BizCenter deja cambiar la preferencia y
/// "Pagar ahora" con step-up, repitiendo EXACTAMENTE el intento (misma clave) cuando no se sabe si
/// entró; en el Admin y suplantando es de solo lectura y no pide saldos.
/// </summary>
public class AnnualFeeCardTests : BunitContext
{
    private sealed class Manejador(Func<HttpRequestMessage, string?, HttpResponseMessage> f) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            var cuerpo = r.Content is null ? null : await r.Content.ReadAsStringAsync(ct);
            return f(r, cuerpo);
        }
    }

    private sealed class ClienteQueConcede : IStepUpClient
    {
        public Task<StepUpOutcome<StepUpChallenge>> RequestChallengeAsync(string o, CancellationToken ct = default) =>
            Task.FromResult(new StepUpOutcome<StepUpChallenge>(true, null,
                new StepUpChallenge("reto", "Email", "n***@x.com", DateTime.Now.AddMinutes(5))));

        public Task<StepUpOutcome<string>> VerifyAsync(string o, string c, string code, CancellationToken ct = default) =>
            Task.FromResult(new StepUpOutcome<string>(true, null, "permiso"));
    }

    private static HttpResponseMessage Json(HttpStatusCode s, string json) =>
        new(s) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private const string Resumen = """
        {"success":true,"data":{"applies":true,"status":"DueSoon","amount":99,"nextDueDate":"2027-06-20T00:00:00",
         "lastPaidDate":null,"canPayNow":true,"notPayableReason":null,"availableFeeTokens":1,"feesFromBalance":true,"recentPayments":[]}}
        """;
    private const string Fondos = """{"success":true,"data":{"commissionBalance":150,"eWalletBalance":null,"hasEWallet":false,"cardEnabled":false}}""";
    private const string Pagado = """{"success":true,"data":{"paymentId":"p1","status":"Completed","method":"Token","amount":0,"nextDueDate":"2028-06-20T00:00:00"}}""";

    private readonly List<(string Metodo, string Ruta, string? Cuerpo, string? Permiso)> _peticiones = new();

    private IRenderedComponent<AnnualFeeCard> Montar(
        Func<HttpRequestMessage, string?, HttpResponseMessage?>? pago = null, bool soloLectura = false,
        bool suplantando = false, string resumen = Resumen)
    {
        var http = new HttpClient(new Manejador((r, cuerpo) =>
        {
            var permiso = r.Headers.TryGetValues(StepUpGrant.HeaderName, out var v) ? v.Single() : null;
            _peticiones.Add((r.Method.Method, r.RequestUri!.AbsolutePath, cuerpo, permiso));
            if (r.Method == HttpMethod.Get && r.RequestUri.AbsolutePath.EndsWith("/billing/annual-fee")) return Json(HttpStatusCode.OK, resumen);
            if (r.Method == HttpMethod.Get && r.RequestUri.AbsolutePath.EndsWith("/wallet/balances")) return Json(HttpStatusCode.OK, Fondos);
            if (r.Method == HttpMethod.Put) return Json(HttpStatusCode.OK, """{"success":true,"data":{"feesFromBalance":false}}""");
            return pago?.Invoke(r, cuerpo) ?? Json(HttpStatusCode.OK, Pagado);
        })) { BaseAddress = new Uri("https://api.pruebas/") };

        Services.AddSharedComponents();
        Services.AddSingleton(http);
        Services.AddSingleton<IStringLocalizer<SharedResources>>(new StepUpDialogTests.LocalizadorDeClaves());
        Services.AddSingleton<IStepUpClient>(new ClienteQueConcede());
        var vista = new Mock<IViewContextService>();
        vista.Setup(v => v.IsImpersonating).Returns(suplantando);
        Services.AddSingleton(vista.Object);
        JSInterop.Mode = JSRuntimeMode.Loose;

        return Render<AnnualFeeCard>(p => p.Add(c => c.ApiBaseUrl, "api/v1/bizcenter").Add(c => c.ReadOnly, soloLectura));
    }

    [Fact]
    public void Enseña_estado_importe_vencimiento_y_tokens()
    {
        var t = Montar();

        t.WaitForAssertion(() => t.Find(".annual-fee-status").TextContent.Should().Contain("AnnualFee.Status.DueSoon"));
        t.Find(".annual-fee-amount").TextContent.Should().Be("$99.00");
        t.Find(".annual-fee-next").TextContent.Should().Be("Jun 20, 2027");
        t.Find(".annual-fee-tokens").TextContent.Should().Be("1");
        t.FindAll(".annual-fee-pay-button").Should().ContainSingle();
    }

    [Fact]
    public void A_un_cliente_le_dice_que_no_aplica()
    {
        var t = Montar(resumen: """{"success":true,"data":{"applies":false,"status":"NotApplicable"}}""");

        t.WaitForAssertion(() => t.Find(".annual-fee-na"));
        t.FindAll(".annual-fee-pay-button").Should().BeEmpty();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void En_solo_lectura_o_suplantando_no_hay_botones_ni_se_piden_saldos(bool soloLectura, bool suplantando)
    {
        var t = Montar(soloLectura: soloLectura, suplantando: suplantando);

        t.WaitForAssertion(() => t.Find(".annual-fee-readonly"));
        t.FindAll(".annual-fee-pay-button").Should().BeEmpty();
        t.FindAll(".annual-fee-preference").Should().BeEmpty();
        _peticiones.Should().NotContain(p => p.Ruta.EndsWith("/wallet/balances"));
    }

    [Fact]
    public async Task Pagar_pide_el_codigo_y_repite_una_vez_con_el_permiso_y_la_misma_clave()
    {
        var intentos = 0;
        var t = Montar(pago: (_, _) => ++intentos == 1
            ? Json(HttpStatusCode.Forbidden, """{"errorCode":"STEP_UP_REQUIRED"}""")
            : Json(HttpStatusCode.OK, Pagado));
        t.WaitForAssertion(() => t.Find(".annual-fee-pay-button"));

        _ = t.Find(".annual-fee-pay-button").ClickAsync(new());
        t.WaitForAssertion(() => t.Find(".stepup-target"));
        t.Find("#stepup-code").Input("123456");
        _ = t.Find(".stepup-confirm").ClickAsync(new());

        t.WaitForAssertion(() => t.Find(".annual-fee-paid"));
        var pagos = _peticiones.Where(p => p.Metodo == "POST").ToList();
        pagos.Should().HaveCount(2);
        pagos[0].Permiso.Should().BeNull();
        pagos[1].Permiso.Should().Be("permiso");
        Clave(pagos[0].Cuerpo).Should().Be(Clave(pagos[1].Cuerpo));
        Metodo(pagos[1].Cuerpo).Should().Be("Token");
        await Task.CompletedTask;
    }

    [Fact]
    public void Si_Billing_no_contesta_el_reintento_repite_la_misma_clave()
    {
        var t = Montar(pago: (_, _) => Json(HttpStatusCode.BadGateway, """{"success":false,"errorCode":"BILLING_UNAVAILABLE"}"""));
        t.WaitForAssertion(() => t.Find(".annual-fee-pay-button"));

        _ = t.Find(".annual-fee-pay-button").ClickAsync(new());
        t.WaitForAssertion(() => t.Find(".annual-fee-error").TextContent.Should().Contain("AnnualFee.Error.Unavailable"));
        _ = t.Find(".annual-fee-pay-button").ClickAsync(new());

        t.WaitForAssertion(() => _peticiones.Count(p => p.Metodo == "POST").Should().Be(2));
        var pagos = _peticiones.Where(p => p.Metodo == "POST").ToList();
        Clave(pagos[0].Cuerpo).Should().Be(Clave(pagos[1].Cuerpo));
    }

    [Fact]
    public void Un_fallo_definitivo_se_traduce_y_el_siguiente_intento_lleva_otra_clave()
    {
        var t = Montar(pago: (_, _) => Json(HttpStatusCode.BadRequest, """{"success":false,"errorCode":"ANNUAL_FEE_NOT_DUE"}"""));
        t.WaitForAssertion(() => t.Find(".annual-fee-pay-button"));

        _ = t.Find(".annual-fee-pay-button").ClickAsync(new());
        t.WaitForAssertion(() => t.Find(".annual-fee-error").TextContent.Should().Contain("AnnualFee.Error.NotDue"));
        _ = t.Find(".annual-fee-pay-button").ClickAsync(new());

        t.WaitForAssertion(() => _peticiones.Count(p => p.Metodo == "POST").Should().Be(2));
        var pagos = _peticiones.Where(p => p.Metodo == "POST").ToList();
        Clave(pagos[0].Cuerpo).Should().NotBe(Clave(pagos[1].Cuerpo));
    }

    [Fact]
    public void Sin_tokens_el_metodo_token_no_se_puede_elegir_y_la_tarjeta_no_aparece()
    {
        var t = Montar(resumen: Resumen.Replace("\"availableFeeTokens\":1", "\"availableFeeTokens\":0"));

        t.WaitForAssertion(() => t.Find("#af-Token").HasAttribute("disabled").Should().BeTrue());
        t.Find("#af-CommissionBalance").HasAttribute("disabled").Should().BeFalse();
        t.Find("#af-EWallet").HasAttribute("disabled").Should().BeTrue();
        t.FindAll("#af-CreditCard").Should().BeEmpty();
        t.Find(".annual-fee-card-soon");
    }

    [Fact]
    public void Cambiar_la_preferencia_la_manda_al_servidor()
    {
        var t = Montar();
        t.WaitForAssertion(() => t.Find(".annual-fee-preference"));

        t.Find(".annual-fee-preference").Change(false);

        t.WaitForAssertion(() => t.Find(".annual-fee-preference-saved"));
        var put = _peticiones.Single(p => p.Metodo == "PUT");
        put.Ruta.Should().Be("/api/v1/bizcenter/billing/annual-fee/preference");
        put.Cuerpo.Should().Contain("\"feesFromBalance\":false");
    }

    private static string Clave(string? cuerpo) =>
        JsonDocument.Parse(cuerpo!).RootElement.GetProperty("idempotencyKey").GetString()!;

    private static string Metodo(string? cuerpo) =>
        JsonDocument.Parse(cuerpo!).RootElement.GetProperty("method").GetString()!;
}
