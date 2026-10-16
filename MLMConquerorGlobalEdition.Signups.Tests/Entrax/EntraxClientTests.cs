using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// El cliente habla el contrato de la documentación de Entrax: ruta, firma, cuerpo, sobre de
/// respuesta {success, data, error} y errores de red. Nunca lanza: devuelve EntraxCall con el código.
/// </summary>
public class EntraxClientTests
{
    private static readonly DateTime UtcFijo = new(2026, 10, 16, 7, 0, 0, DateTimeKind.Utc);
    private const string PaymentId = "550e8400-e29b-41d4-a716-446655440000";

    private sealed class Manejador(Func<HttpRequestMessage, string, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Peticion;
        public string Cuerpo = string.Empty;
        public int Llamadas;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Llamadas++;
            Peticion = r;
            Cuerpo = r.Content is null ? string.Empty : await r.Content.ReadAsStringAsync(ct);
            return responder(r, Cuerpo);
        }
    }

    private sealed class Caido : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => Task.FromException<HttpResponseMessage>(new TaskCanceledException("timeout"));
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string json)
        => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static EntraxOptions Opciones() => new()
    {
        Enabled = true, KeyId = "key-1", Secret = "secreto-de-pruebas",
        CallbackUrl = "https://alta.pruebas/api/v1/signups/entrax/callback"
    };

    private static EntraxClient Cliente(HttpMessageHandler h, Action<EntraxOptions>? ajustar = null)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(c => c.UtcNow).Returns(UtcFijo);
        var o = Opciones();
        ajustar?.Invoke(o);
        return new EntraxClient(new HttpClient(h), o, reloj.Object, NullLogger<EntraxClient>.Instance);
    }

    /// <summary>Un cuerpo de respuesta que revienta al leerlo (conexión cortada a media respuesta).</summary>
    private sealed class CuerpoRoto : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
            => Task.FromException(new IOException("conexión cortada"));

        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }

    /// <summary>"HMAC-SHA256 KeyId=…,Timestamp=…,Nonce=…,Signature=…" → diccionario. La firma Base64 puede llevar '='.</summary>
    private static Dictionary<string, string> Partes(HttpRequestMessage r)
        => r.Headers.GetValues("Authorization").Single()["HMAC-SHA256 ".Length..]
            .Split(',').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => p[1]);

    private const string QrOk = """
        {"success":true,"data":{
          "qrCode":{"image":"data:image/png;base64,iVBORw0KGgo=","link":"https://qr.nspk.ru/AS100","paymentId":"550e8400-e29b-41d4-a716-446655440000"},
          "payment":{"amountInRubles":7525.50,"exchangeRate":75.255,"currency":"USD","originalAmount":100.50,"expiresAt":"2026-10-16T07:15:00Z"},
          "order":{"orderId":"SIGNUP-o1-1","status":"pending"}},
         "requestId":"r1"}
        """;

    [Fact]
    public async Task Pide_el_QR_con_POST_firmado_a_la_ruta_de_la_caja()
    {
        var h = new Manejador((_, _) => Json(HttpStatusCode.OK, QrOk));

        await Cliente(h).CreateQrAsync(
            new EntraxQrRequest(100.50m, "USD", "SIGNUP-o1-1", "MLM Conqueror signup", "https://alta.pruebas/cb"), default);

        h.Peticion!.Method.Should().Be(HttpMethod.Post);
        h.Peticion.RequestUri!.ToString().Should().Be("https://entrax.pro/api/v1/cash-register/qr");

        using var doc = JsonDocument.Parse(h.Cuerpo);
        doc.RootElement.GetProperty("amount").GetDecimal().Should().Be(100.50m);
        doc.RootElement.GetProperty("currency").GetString().Should().Be("USD");
        doc.RootElement.GetProperty("orderId").GetString().Should().Be("SIGNUP-o1-1");
        doc.RootElement.GetProperty("callbackUrl").GetString().Should().Be("https://alta.pruebas/cb");

        // La firma se recalcula con las piezas de la cabecera: si coincide, se firmó lo que se mandó.
        h.Peticion.Headers.GetValues("Authorization").Single().Should().StartWith("HMAC-SHA256 KeyId=key-1,");
        var partes = Partes(h.Peticion);
        partes["Timestamp"].Should().Be(new DateTimeOffset(UtcFijo).ToUnixTimeSeconds().ToString());
        var canonica = EntraxSignature.Canonical("POST", "/api/v1/cash-register/qr",
            long.Parse(partes["Timestamp"]), partes["Nonce"], h.Cuerpo);
        partes["Signature"].Should().Be(EntraxSignature.Sign(canonica, "secreto-de-pruebas"));
    }

    [Fact]
    public async Task Lee_el_QR_de_la_respuesta()
    {
        var r = await Cliente(new Manejador((_, _) => Json(HttpStatusCode.OK, QrOk)))
            .CreateQrAsync(new EntraxQrRequest(100.50m, "USD", "SIGNUP-o1-1", null, null), default);

        r.Ok.Should().BeTrue();
        r.Value!.PaymentId.Should().Be(PaymentId);
        r.Value.Link.Should().Be("https://qr.nspk.ru/AS100");
        r.Value.ImageDataUri.Should().StartWith("data:image/png;base64,");
        r.Value.AmountInRubles.Should().Be(7525.50m);
        r.Value.ExchangeRate.Should().Be(75.255m);
        r.Value.Currency.Should().Be("USD");
        r.Value.OriginalAmount.Should().Be(100.50m);
        r.Value.ExpiresAt.Should().Be(new DateTimeOffset(2026, 10, 16, 7, 15, 0, TimeSpan.Zero));
        r.HttpStatus.Should().Be(200);
    }

    [Fact]
    public async Task Un_error_de_Entrax_devuelve_su_codigo()
    {
        var r = await Cliente(new Manejador((_, _) => Json(HttpStatusCode.BadRequest,
                """{"success":false,"error":{"code":"RATE_NOT_AVAILABLE","message":"No rate"},"requestId":"r"}""")))
            .CreateQrAsync(new EntraxQrRequest(10m, "USD", "SIGNUP-o1-1", null, null), default);

        r.Ok.Should().BeFalse();
        r.ErrorCode.Should().Be("RATE_NOT_AVAILABLE");
        r.HttpStatus.Should().Be(400);
    }

    [Fact]
    public async Task Sin_respuesta_es_TRANSPORT_y_no_lanza()
    {
        var r = await Cliente(new Caido())
            .CreateQrAsync(new EntraxQrRequest(10m, "USD", "SIGNUP-o1-1", null, null), default);

        r.Ok.Should().BeFalse();
        r.ErrorCode.Should().Be(EntraxErrorCodes.Transport);
        r.HttpStatus.Should().Be(0);
    }

    [Fact]
    public async Task Una_respuesta_que_no_es_JSON_es_BAD_RESPONSE()
    {
        var r = await Cliente(new Manejador((_, _) => new HttpResponseMessage(HttpStatusCode.BadGateway)
            { Content = new StringContent("<html>502</html>") }))
            .CreateQrAsync(new EntraxQrRequest(10m, "USD", "SIGNUP-o1-1", null, null), default);

        r.ErrorCode.Should().Be(EntraxErrorCodes.BadResponse);
        r.HttpStatus.Should().Be(502);
    }

    [Fact]
    public async Task Consulta_el_pago_con_GET_firmado_y_cuerpo_vacio()
    {
        var h = new Manejador((_, _) => Json(HttpStatusCode.OK, """
            {"success":true,"data":{"paymentId":"550e8400-e29b-41d4-a716-446655440000","status":"paid","orderId":"SIGNUP-o1-1",
             "amount":{"original":100.50,"currency":"USD","rubles":7525.50,"exchangeRate":75.255},
             "timestamps":{"created":"2026-10-16T07:00:00Z","completed":"2026-10-16T07:05:30Z"},
             "transactionDetails":{"sbpTransactionId":null,"payerPhone":"+7 900 000 00 00"}}}
            """));

        var r = await Cliente(h).GetPaymentAsync(PaymentId, default);

        h.Peticion!.Method.Should().Be(HttpMethod.Get);
        h.Peticion.RequestUri!.AbsolutePath.Should().Be($"/api/v1/cash-register/payment/{PaymentId}");
        h.Peticion.Content.Should().BeNull();
        var partes = Partes(h.Peticion);
        var canonica = EntraxSignature.Canonical("GET", $"/api/v1/cash-register/payment/{PaymentId}",
            long.Parse(partes["Timestamp"]), partes["Nonce"], string.Empty);
        partes["Signature"].Should().Be(EntraxSignature.Sign(canonica, "secreto-de-pruebas"));

        r.Ok.Should().BeTrue();
        r.Value!.Status.Should().Be("paid");
        r.Value.OrderId.Should().Be("SIGNUP-o1-1");
        r.Value.Original.Should().Be(100.50m);
        r.Value.Currency.Should().Be("USD");
        r.Value.CompletedAt.Should().Be(new DateTimeOffset(2026, 10, 16, 7, 5, 30, TimeSpan.Zero));
    }

    [Theory]
    [InlineData("../rates")]
    [InlineData("no-es-un-guid")]
    [InlineData("")]
    public async Task Un_paymentId_que_no_es_GUID_no_sale_a_la_red(string id)
    {
        var h = new Manejador((_, _) => Json(HttpStatusCode.OK, "{}"));

        var r = await Cliente(h).GetPaymentAsync(id, default);

        r.ErrorCode.Should().Be(EntraxErrorCodes.InvalidPaymentId);
        h.Llamadas.Should().Be(0);
    }

    // ── El cliente nunca lanza ───────────────────────────────────────────────────

    public static TheoryData<string, Action<EntraxOptions>> MalConfigurado => new()
    {
        { "sin KeyId",       o => o.KeyId = "" },
        { "sin Secret",      o => o.Secret = null },
        { "BaseUrl http",    o => o.BaseUrl = "http://entrax.pro/api/v1/cash-register" },
        { "BaseUrl relativa", o => o.BaseUrl = "api/v1/cash-register" },
        { "BaseUrl basura",  o => o.BaseUrl = "::no es una url::" }
    };

    [Theory]
    [MemberData(nameof(MalConfigurado))]
    public async Task Mal_configurado_no_sale_a_la_red_ni_lanza(string caso, Action<EntraxOptions> ajustar)
    {
        var h = new Manejador((_, _) => Json(HttpStatusCode.OK, QrOk));
        var cliente = Cliente(h, ajustar);

        var qr = await cliente.CreateQrAsync(new EntraxQrRequest(10m, "USD", "SIGNUP-o1-1", null, null), default);
        var pago = await cliente.GetPaymentAsync(PaymentId, default);

        qr.ErrorCode.Should().Be(EntraxErrorCodes.NotConfigured, caso);
        qr.HttpStatus.Should().Be(0);
        pago.ErrorCode.Should().Be(EntraxErrorCodes.NotConfigured, caso);
        h.Llamadas.Should().Be(0, "sin credenciales completas o sin https no se manda nada firmado a ningún sitio.");
    }

    [Fact]
    public async Task Si_el_cuerpo_de_la_respuesta_revienta_al_leerlo_es_TRANSPORT_y_no_lanza()
    {
        var r = await Cliente(new Manejador((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new CuerpoRoto() }))
            .GetPaymentAsync(PaymentId, default);

        r.Ok.Should().BeFalse();
        r.ErrorCode.Should().Be(EntraxErrorCodes.Transport);
    }

    [Fact]
    public async Task Una_excepcion_inesperada_del_manejador_tampoco_sale_del_cliente()
    {
        var r = await Cliente(new Manejador((_, _) => throw new InvalidOperationException("handler roto")))
            .GetPaymentAsync(PaymentId, default);

        r.Ok.Should().BeFalse();
        r.ErrorCode.Should().Be(EntraxErrorCodes.Transport);
    }
}
