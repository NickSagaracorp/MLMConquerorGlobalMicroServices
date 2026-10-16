using System.Net;
using System.Text;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MLMConquerorGlobalEdition.Signups.Client.Payments;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// El flujo del QR que comparten Signup.razor y MemberJoin.razor: enseña el formulario hasta que el
/// servidor emite un QR, y avisa a la página de lo que pasa (alta ya pagada, error, QR vivo en otra ventana).
/// </summary>
public class EntraxSignupFlowTests : BunitContext
{
    private const string QrOk =
        """{"success":true,"data":{"signupId":"ORD-1","email":"a@x.com","entrax":{"paymentRef":"ref-1","qrLink":"https://qr.nspk.ru/A","amountRub":7640,"amountUsd":80,"secondsRemaining":900}}}""";

    private sealed class Fabrica(Func<HttpRequestMessage, HttpResponseMessage> responder) : IHttpClientFactory
    {
        public int Peticiones;

        private sealed class Manejador(Fabrica f, Func<HttpRequestMessage, HttpResponseMessage> r) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            {
                Interlocked.Increment(ref f.Peticiones);
                return Task.FromResult(r(req));
            }
        }

        public HttpClient CreateClient(string name)
            => new(new Manejador(this, responder)) { BaseAddress = new Uri("https://signup.pruebas/") };
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string json)
        => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string Fallo(string codigo, string texto)
        => $$"""{"success":false,"errorCode":"{{codigo}}","errors":["{{texto}}"]}""";

    private sealed record Avisos(List<bool> Completadas, List<string> Errores, int[] OtroMetodo);

    private (IRenderedComponent<EntraxSignupFlow> Cut, Avisos Avisos, Fabrica Http) Pinta(
        Func<HttpRequestMessage, HttpResponseMessage> responder, string? signupId = "ORD-1")
    {
        var http = new Fabrica(responder);
        Services.AddSingleton<IHttpClientFactory>(http);
        var avisos = new Avisos([], [], [0]);
        var cut = Render<EntraxSignupFlow>(p => p
            .Add(c => c.SignupId, signupId)
            .AddChildContent("<form id='formulario'>formulario del alta</form>")
            .Add(c => c.OnCompleted, EventCallback.Factory.Create<bool>(this, v => avisos.Completadas.Add(v)))
            .Add(c => c.OnError, EventCallback.Factory.Create<string>(this, t => avisos.Errores.Add(t)))
            .Add(c => c.OnChooseAnother, EventCallback.Factory.Create(this, () => avisos.OtroMetodo[0]++)));
        return (cut, avisos, http);
    }

    [Fact]
    public void Sin_QR_ensena_el_formulario()
    {
        var (cut, _, _) = Pinta(_ => Json(HttpStatusCode.OK, QrOk));

        cut.Find("#formulario").TextContent.Should().Contain("formulario del alta");
        cut.FindAll(".entrax-qr").Should().BeEmpty();
        cut.Instance.HasPayment.Should().BeFalse();
    }

    [Fact]
    public async Task Si_el_servidor_emite_el_QR_lo_ensena_en_lugar_del_formulario()
    {
        var (cut, avisos, _) = Pinta(_ => Json(HttpStatusCode.OK, QrOk));

        await cut.InvokeAsync(() => cut.Instance.StartAsync());

        cut.FindAll("#formulario").Should().BeEmpty();
        cut.Find("img.entrax-qr-image, a.entrax-link").Should().NotBeNull();
        cut.Find(".entrax-amount").TextContent.Should().Contain("7,640.00");
        cut.Instance.HasPayment.Should().BeTrue();
        avisos.Errores.Should().BeEmpty();
        avisos.Completadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_alta_ya_pagada_avisa_a_la_pagina_y_no_ensena_ningun_QR()
    {
        var (cut, avisos, http) = Pinta(_ => Json(HttpStatusCode.Conflict, Fallo("SIGNUP_ALREADY_SETTLED", "ya")));

        await cut.InvokeAsync(() => cut.Instance.StartAsync());

        avisos.Completadas.Should().Equal(true);
        avisos.Errores.Should().BeEmpty();
        cut.FindAll(".entrax-qr").Should().BeEmpty();
        http.Peticiones.Should().Be(1, "un alta ya pagada no se reintenta.");
    }

    [Fact]
    public async Task Con_un_QR_vivo_en_otra_ventana_da_el_aviso_y_no_reintenta()
    {
        var (cut, avisos, http) = Pinta(_ => Json(HttpStatusCode.Conflict, Fallo("ENTRAX_QR_ALREADY_ACTIVE", "x")));

        await cut.InvokeAsync(() => cut.Instance.StartAsync());

        avisos.Errores.Should().Equal("There is already an active QR for this signup in another window.");
        avisos.Completadas.Should().BeEmpty();
        cut.FindAll(".entrax-qr").Should().BeEmpty();
        http.Peticiones.Should().Be(1);
    }

    [Fact]
    public async Task Un_error_del_servidor_llega_como_texto_y_se_sigue_viendo_el_formulario()
    {
        var (cut, avisos, _) = Pinta(_ => Json(HttpStatusCode.BadGateway, Fallo("ENTRAX_UNAVAILABLE", "No se pudo crear el pago SBP.")));

        await cut.InvokeAsync(() => cut.Instance.StartAsync());

        avisos.Errores.Should().Equal("No se pudo crear el pago SBP.");
        cut.Find("#formulario").Should().NotBeNull();
    }

    [Fact]
    public async Task Un_fallo_de_conexion_llega_como_texto()
    {
        var (cut, avisos, _) = Pinta(_ => throw new HttpRequestException("caido"));

        await cut.InvokeAsync(() => cut.Instance.StartAsync());

        avisos.Errores.Should().ContainSingle().Which.Should().StartWith("Connection error:").And.Contain("caido");
    }

    [Fact]
    public async Task Sin_id_de_alta_no_hace_nada()
    {
        var (cut, avisos, http) = Pinta(_ => Json(HttpStatusCode.OK, QrOk), signupId: null);

        await cut.InvokeAsync(() => cut.Instance.StartAsync());

        http.Peticiones.Should().Be(0);
        avisos.Errores.Should().BeEmpty();
    }

    [Fact]
    public async Task Elegir_otro_metodo_quita_el_QR_y_avisa_a_la_pagina()
    {
        var (cut, avisos, _) = Pinta(_ => Json(HttpStatusCode.OK, QrOk));
        await cut.InvokeAsync(() => cut.Instance.StartAsync());

        // Los botones de "otro método" solo aparecen con el QR caducado (lo prueba EntraxQrPayment, con reloj
        // manual); aquí se lanza su evento tal cual para comprobar que el flujo lo reenvía y limpia el QR.
        await cut.InvokeAsync(() => cut.FindComponent<MLMConquerorGlobalEdition.SharedComponents.Lite.Payments.EntraxQrPayment>()
            .Instance.OnChooseAnother.InvokeAsync());

        avisos.OtroMetodo[0].Should().Be(1);
        cut.FindAll(".entrax-qr").Should().BeEmpty();
        cut.Find("#formulario").Should().NotBeNull();
    }

    [Fact]
    public async Task El_reintento_del_QR_pide_otro_y_lo_cambia()
    {
        var n = 0;
        var (cut, _, http) = Pinta(_ => Json(HttpStatusCode.OK,
            QrOk.Replace("ref-1", ++n == 1 ? "ref-1" : "ref-2")));
        await cut.InvokeAsync(() => cut.Instance.StartAsync());

        await cut.InvokeAsync(() => cut.FindComponent<MLMConquerorGlobalEdition.SharedComponents.Lite.Payments.EntraxQrPayment>()
            .Instance.OnRetry.InvokeAsync());

        http.Peticiones.Should().Be(2);
        cut.FindComponent<MLMConquerorGlobalEdition.SharedComponents.Lite.Payments.EntraxQrPayment>()
            .Instance.Payment.PaymentRef.Should().Be("ref-2");
    }

    [Fact]
    public async Task Si_el_reintento_dice_que_ya_estaba_pagada_avisa_y_quita_el_QR()
    {
        var n = 0;
        var (cut, avisos, _) = Pinta(_ => ++n == 1
            ? Json(HttpStatusCode.OK, QrOk)
            : Json(HttpStatusCode.Conflict, Fallo("SIGNUP_ALREADY_SETTLED", "ya")));
        await cut.InvokeAsync(() => cut.Instance.StartAsync());

        await cut.InvokeAsync(() => cut.FindComponent<MLMConquerorGlobalEdition.SharedComponents.Lite.Payments.EntraxQrPayment>()
            .Instance.OnRetry.InvokeAsync());

        avisos.Completadas.Should().Equal(true);
        cut.FindAll(".entrax-qr").Should().BeEmpty();
    }
}
