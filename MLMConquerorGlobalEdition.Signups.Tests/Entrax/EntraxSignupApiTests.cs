using System.Net;
using System.Text;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.Signups.Client.Payments;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>Las tres llamadas del alta con Entrax que comparten Signup.razor y MemberJoin.razor.</summary>
public class EntraxSignupApiTests
{
    private sealed class Api(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public readonly List<HttpRequestMessage> Peticiones = [];
        public HttpRequestMessage? Peticion => Peticiones.LastOrDefault();
        public string? Cuerpo;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Peticiones.Add(r);
            Cuerpo = r.Content is null ? null : await r.Content.ReadAsStringAsync(ct);
            return responder(r);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string json)
        => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpClient Cliente(HttpMessageHandler h) => new(h) { BaseAddress = new Uri("https://signup.pruebas/") };

    private const string QrOk =
        """{"success":true,"data":{"signupId":"ORD 1","email":"a@x.com","entrax":{"paymentRef":"ref-1","qrLink":"https://qr.nspk.ru/A","amountRub":7640,"amountUsd":80,"secondsRemaining":900}}}""";

    private static string Fallo(string codigo, string texto)
        => $$"""{"success":false,"errorCode":"{{codigo}}","errors":["{{texto}}"]}""";

    [Fact]
    public void Los_codigos_que_distingue_la_pantalla_son_los_del_servidor()
    {
        EntraxSignupApi.InProgressCode.Should().Be(EntraxSignupErrors.InProgress);
        EntraxSignupApi.AlreadySettledCode.Should().Be(CompleteSignupHandler.SignupAlreadySettledCode);
    }

    [Fact]
    public async Task Lee_los_paises_donde_se_ofrece()
    {
        var api = new Api(_ => Json(HttpStatusCode.OK, """{"success":true,"data":{"entraxCountries":["RU"]}}"""));

        (await EntraxSignupApi.GetCountriesAsync(Cliente(api))).Should().Equal("RU");
        api.Peticion!.RequestUri!.AbsolutePath.Should().Be("/api/v1/signups/payment-options");
    }

    [Fact]
    public async Task Si_no_se_puede_leer_no_se_ofrece()
        => (await EntraxSignupApi.GetCountriesAsync(Cliente(new Api(_ => throw new HttpRequestException("caido")))))
            .Should().BeEmpty();

    [Fact]
    public async Task Si_el_servidor_contesta_con_error_no_se_ofrece()
        => (await EntraxSignupApi.GetCountriesAsync(Cliente(new Api(_ => Json(HttpStatusCode.TooManyRequests, "{}")))))
            .Should().BeEmpty();

    [Fact]
    public async Task Completa_el_alta_con_Entrax_y_devuelve_el_QR()
    {
        var api = new Api(_ => Json(HttpStatusCode.OK, QrOk));

        var r = await EntraxSignupApi.StartAsync(Cliente(api), "ORD 1");

        r.Outcome.Should().Be(EntraxStartOutcome.Started);
        r.Error.Should().BeNull();
        r.Payment!.PaymentRef.Should().Be("ref-1");
        r.Payment.AmountRub.Should().Be(7640m);
        api.Peticion!.Method.Should().Be(HttpMethod.Post);
        api.Peticion.RequestUri!.AbsolutePath.Should().Be("/api/v1/signups/ORD%201/complete");
        api.Cuerpo.Should().Contain("\"paymentMethod\":\"Entrax\"", "PostAsJsonAsync usa camelCase; SignupAPI enlaza sin distinguir mayúsculas.");
    }

    [Fact]
    public async Task Un_error_del_alta_llega_como_texto()
    {
        var api = new Api(_ => Json(HttpStatusCode.BadGateway,
            Fallo("ENTRAX_UNAVAILABLE", "We couldn't create the SBP payment right now.")));

        var r = await EntraxSignupApi.StartAsync(Cliente(api), "ORD-1");

        r.Outcome.Should().Be(EntraxStartOutcome.Failed);
        r.Payment.Should().BeNull();
        r.Error.Should().Be("We couldn't create the SBP payment right now.");
        api.Peticiones.Should().HaveCount(1, "un 502 no se reintenta solo: el aspirante decide.");
    }

    [Fact]
    public async Task Una_respuesta_que_no_es_JSON_no_revienta()
    {
        var api = new Api(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            { Content = new StringContent("Too many requests", Encoding.UTF8, "text/plain") });

        var r = await EntraxSignupApi.StartAsync(Cliente(api), "ORD-1");

        r.Outcome.Should().Be(EntraxStartOutcome.Failed);
        r.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Un_alta_ya_pagada_termina_enseguida_y_sin_reintento()
    {
        var api = new Api(_ => Json(HttpStatusCode.Conflict,
            Fallo("SIGNUP_ALREADY_SETTLED", "This enrollment is already paid.")));

        var r = await EntraxSignupApi.StartAsync(Cliente(api), "ORD-1", retryDelay: TimeSpan.Zero);

        r.Outcome.Should().Be(EntraxStartOutcome.AlreadySettled);
        r.Payment.Should().BeNull();
        api.Peticiones.Should().HaveCount(1);
    }

    [Fact]
    public async Task Si_otra_peticion_esta_creando_el_QR_espera_y_reintenta()
    {
        var n = 0;
        var api = new Api(_ => ++n < 3
            ? Json(HttpStatusCode.Conflict, Fallo("ENTRAX_IN_PROGRESS", "busy"))
            : Json(HttpStatusCode.OK, QrOk));

        var r = await EntraxSignupApi.StartAsync(Cliente(api), "ORD-1", retryDelay: TimeSpan.Zero);

        r.Outcome.Should().Be(EntraxStartOutcome.Started);
        api.Peticiones.Should().HaveCount(3);
    }

    [Fact]
    public async Task Si_sigue_ocupado_tras_los_intentos_lo_dice_y_no_insiste()
    {
        var api = new Api(_ => Json(HttpStatusCode.Conflict, Fallo("ENTRAX_IN_PROGRESS", "busy")));

        var r = await EntraxSignupApi.StartAsync(Cliente(api), "ORD-1", retryDelay: TimeSpan.Zero);

        r.Outcome.Should().Be(EntraxStartOutcome.InProgress);
        r.Error.Should().Contain("try again");
        api.Peticiones.Should().HaveCount(EntraxSignupApi.InProgressAttempts);
    }

    [Fact]
    public async Task Esperar_para_reintentar_respeta_la_cancelacion()
    {
        var api = new Api(_ => Json(HttpStatusCode.Conflict, Fallo("ENTRAX_IN_PROGRESS", "busy")));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => EntraxSignupApi.StartAsync(Cliente(api), "ORD-1", cts.Token, retryDelay: TimeSpan.FromMinutes(1));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Pregunta_el_estado_con_las_dos_referencias()
    {
        var api = new Api(_ => Json(HttpStatusCode.OK, """{"success":true,"data":{"status":"Paid","secondsRemaining":0}}"""));

        var s = await EntraxSignupApi.GetStatusAsync(Cliente(api), "ORD-1", "ref/1", default);

        s!.Status.Should().Be(EntraxSignupStatuses.Paid);
        api.Peticion!.RequestUri!.PathAndQuery.Should().Be("/api/v1/signups/ORD-1/entrax-status?ref=ref%2F1");
    }

    [Fact]
    public async Task Lee_el_estado_Superseded()
    {
        var api = new Api(_ => Json(HttpStatusCode.OK, """{"success":true,"data":{"status":"Superseded","secondsRemaining":0}}"""));

        (await EntraxSignupApi.GetStatusAsync(Cliente(api), "ORD-1", "r", default))!.Status
            .Should().Be(EntraxSignupStatuses.Superseded);
    }

    [Fact]
    public async Task Un_estado_que_no_existe_es_null()
        => (await EntraxSignupApi.GetStatusAsync(Cliente(new Api(_ => Json(HttpStatusCode.NotFound, "{}"))), "ORD-1", "x", default))
            .Should().BeNull();

    [Fact]
    public async Task Un_429_al_preguntar_el_estado_es_null_y_la_pantalla_sigue_esperando()
        => (await EntraxSignupApi.GetStatusAsync(Cliente(new Api(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests))), "ORD-1", "x", default))
            .Should().BeNull();
}
