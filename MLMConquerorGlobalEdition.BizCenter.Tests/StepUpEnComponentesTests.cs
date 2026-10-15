using System.Net;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp;
using MLMConquerorGlobalEdition.SharedComponents.Components.Profile;
using MLMConquerorGlobalEdition.SharedComponents.Extensions;
using MLMConquerorGlobalEdition.SharedComponents.Resources;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// El camino completo de un componente real (CommissionsCard) con step-up: el 403 abre el código y,
/// si el reintento vuelve a fallar por step-up, la persona ve el texto traducido y no el cuerpo crudo.
/// </summary>
public class StepUpEnComponentesTests : BunitContext
{
    private sealed class Manejador(Func<HttpRequestMessage, HttpResponseMessage> f) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) =>
            Task.FromResult(f(r));
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

    private const string Wallets = """
        {"success":true,"data":[
          {"walletId":"a","walletType":1,"walletTypeName":"A","status":2,"statusName":"Approved","accountIdentifier":"x","isPreferred":true},
          {"walletId":"b","walletType":2,"walletTypeName":"B","status":2,"statusName":"Approved","accountIdentifier":"y","isPreferred":false}]}
        """;

    [Fact]
    public async Task Si_el_reintento_vuelve_a_dar_STEP_UP_INVALID_se_muestra_RetryFailed()
    {
        var puts = 0;
        var http = new HttpClient(new Manejador(req =>
        {
            if (req.Method == HttpMethod.Get) return Json(HttpStatusCode.OK, Wallets);
            puts++;
            return Json(HttpStatusCode.Forbidden, puts == 1
                ? "{\"errorCode\":\"STEP_UP_REQUIRED\"}"
                : "{\"errorCode\":\"STEP_UP_INVALID\"}");
        })) { BaseAddress = new Uri("https://api.pruebas/") };

        Services.AddSharedComponents();
        Services.AddSingleton(http);
        Services.AddSingleton<IStringLocalizer<SharedResources>>(new StepUpDialogTests.LocalizadorDeClaves());
        Services.AddSingleton<IStepUpClient>(new ClienteQueConcede());

        var tarjeta = Render<CommissionsCard>(p => p
            .Add(c => c.CanEdit, true).Add(c => c.CurrentFrequency, "Weekly").Add(c => c.MemberProfileId, "m1"));
        tarjeta.WaitForAssertion(() => tarjeta.Find("button.btn-outline-primary"));
        await tarjeta.Find("button.btn-outline-primary").ClickAsync(new());

        tarjeta.Find("#commPayoutMethod").Change("2");
        _ = tarjeta.Find("button.btn-primary").ClickAsync(new());   // 1.º PUT -> pide código (no se espera: SaveAsync queda esperando el código)
        tarjeta.WaitForAssertion(() => tarjeta.Find(".stepup-target"));
        tarjeta.Find("#stepup-code").Input("123456");
        _ = tarjeta.Find(".stepup-confirm").ClickAsync(new());       // 2.º PUT -> STEP_UP_INVALID

        tarjeta.WaitForAssertion(() =>
            tarjeta.Markup.Should().Contain("StepUp.RetryFailed").And.NotContain("STEP_UP_INVALID"));
        puts.Should().Be(2);
    }
}
