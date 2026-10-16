using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// El callback de Entrax: sin firma o con firma mala no entra; con firma buena solo sirve para
/// disparar la liquidación (que vuelve a preguntar a Entrax). 2xx solo cuando se procesó.
/// </summary>
public class EntraxCallbackProcessorTests
{
    private const string Secreto = "secreto-de-pruebas";

    private static long Ahora() => new DateTimeOffset(DateTime.SpecifyKind(EscenarioEntrax.Ahora, DateTimeKind.Utc)).ToUnixTimeSeconds();

    private static byte[] Cuerpo(string paymentId = EscenarioEntrax.PaymentId, string? orderId = null,
        string status = "SUCCESS", long? timestamp = null)
        => Encoding.UTF8.GetBytes(
            $$"""{"paymentId":"{{paymentId}}","orderId":"{{orderId ?? EscenarioEntrax.ExternalOrderId()}}","status":"{{status}}","money":{"summary":80,"currency":"USD","amount":7640,"rate":95.5},"timestamp":{{timestamp ?? Ahora()}}}""");

    private static string Firma(byte[] cuerpo, string secreto = Secreto)
        => Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), cuerpo));

    private sealed record Montaje(AppDbContext Db, Mock<IEntraxSignupSettlement> Liquidacion, EntraxCallbackProcessor Proceso);

    private static async Task<Montaje> MontarAsync(
        EntraxSettlementOutcome resultado = EntraxSettlementOutcome.Activated,
        Action<EntraxOptions>? opciones = null,
        string? paymentIdGuardado = EscenarioEntrax.PaymentId)
    {
        var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        await EscenarioEntrax.SembrarPagoAsync(db, paymentId: paymentIdGuardado);

        var liquidacion = new Mock<IEntraxSignupSettlement>();
        liquidacion.Setup(s => s.SettleAsync(It.IsAny<string>(), It.IsAny<EntraxSettlementSource>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(resultado);

        var proceso = new EntraxCallbackProcessor(db, liquidacion.Object, EscenarioEntrax.Opciones(opciones),
            EscenarioEntrax.Reloj().Object, NullLogger<EntraxCallbackProcessor>.Instance);
        return new Montaje(db, liquidacion, proceso);
    }

    private static void NoSeLiquido(Montaje m)
        => m.Liquidacion.Verify(s => s.SettleAsync(It.IsAny<string>(), It.IsAny<EntraxSettlementSource>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task Sin_secreto_configurado_se_rechaza_aunque_venga_firmado()
    {
        var m = await MontarAsync(opciones: o => o.Secret = null);
        var cuerpo = Cuerpo();

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(EntraxCallbackOutcome.Unauthorized);
        NoSeLiquido(m);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Sin_firma_se_rechaza(string? firma)
    {
        var m = await MontarAsync();

        (await m.Proceso.ProcessAsync(Cuerpo(), firma, default)).Should().Be(EntraxCallbackOutcome.Unauthorized,
            "si Entrax no tiene clave manda el callback sin firma: no se acepta.");
        NoSeLiquido(m);
    }

    [Fact]
    public async Task Con_firma_de_otro_secreto_se_rechaza()
    {
        var m = await MontarAsync();
        var cuerpo = Cuerpo();

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo, "otro"), default)).Should().Be(EntraxCallbackOutcome.Unauthorized);
        NoSeLiquido(m);
    }

    [Fact]
    public async Task Con_firma_valida_liquida_por_el_camino_unico_y_anota_el_aviso()
    {
        var m = await MontarAsync();
        var pago = await m.Db.EntraxPayments.SingleAsync();
        var cuerpo = Cuerpo();

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(EntraxCallbackOutcome.Accepted);

        m.Liquidacion.Verify(s => s.SettleAsync(pago.Id, EntraxSettlementSource.Webhook, It.IsAny<CancellationToken>()), Times.Once);
        var aviso = await m.Db.EntraxApiCalls.SingleAsync();
        aviso.Operation.Should().Be("Callback");
        aviso.ProviderStatus.Should().Be("SUCCESS");
    }

    [Fact]
    public async Task La_firma_en_hexadecimal_tambien_vale()
    {
        var m = await MontarAsync();
        var cuerpo = Cuerpo();
        var hex = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secreto), cuerpo));

        (await m.Proceso.ProcessAsync(cuerpo, hex, default)).Should().Be(EntraxCallbackOutcome.Accepted);
    }

    [Fact]
    public async Task Un_aviso_de_hace_dos_horas_se_rechaza_sin_tocar_nada()
    {
        var m = await MontarAsync();
        var cuerpo = Cuerpo(timestamp: Ahora() - 2 * 3600);

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(EntraxCallbackOutcome.Stale);
        NoSeLiquido(m);
    }

    [Fact]
    public async Task Un_reintento_de_hace_35_minutos_todavia_entra()
    {
        var m = await MontarAsync();
        var cuerpo = Cuerpo(timestamp: Ahora() - 35 * 60);

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(EntraxCallbackOutcome.Accepted,
            "Entrax reintenta durante 35 minutos.");
    }

    [Fact]
    public async Task Un_timestamp_en_milisegundos_se_entiende()
    {
        var m = await MontarAsync();
        var cuerpo = Cuerpo(timestamp: Ahora() * 1000);

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(EntraxCallbackOutcome.Accepted);
    }

    [Fact]
    public async Task Un_pago_que_no_es_nuestro_es_404_para_que_Entrax_reintente()
    {
        var m = await MontarAsync();
        var cuerpo = Cuerpo(paymentId: Guid.NewGuid().ToString(), orderId: "SIGNUP-OTRO-1");

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(EntraxCallbackOutcome.NotFound);
        NoSeLiquido(m);
    }

    [Fact]
    public async Task Si_aun_no_teniamos_el_paymentId_se_encuentra_por_nuestro_orderId()
    {
        var m = await MontarAsync(paymentIdGuardado: null);
        var cuerpo = Cuerpo(paymentId: EscenarioEntrax.PaymentId.ToUpperInvariant());

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(EntraxCallbackOutcome.Accepted);

        (await m.Db.EntraxPayments.SingleAsync()).ExternalPaymentId.Should().Be(EscenarioEntrax.PaymentId);
    }

    [Fact]
    public async Task Un_orderId_que_no_es_el_del_pago_es_invalido()
    {
        var m = await MontarAsync();
        var cuerpo = Cuerpo(orderId: "SIGNUP-OTRO-1");

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(EntraxCallbackOutcome.Invalid);
        NoSeLiquido(m);
    }

    [Fact]
    public async Task Un_JSON_roto_con_firma_valida_es_invalido()
    {
        var m = await MontarAsync();
        var cuerpo = Encoding.UTF8.GetBytes("{\"paymentId\":");

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(EntraxCallbackOutcome.Invalid);
    }

    [Theory]
    [InlineData(EntraxSettlementOutcome.Activated, EntraxCallbackOutcome.Accepted)]
    [InlineData(EntraxSettlementOutcome.AlreadyResolved, EntraxCallbackOutcome.Accepted)]
    [InlineData(EntraxSettlementOutcome.ToReview, EntraxCallbackOutcome.Accepted)]
    [InlineData(EntraxSettlementOutcome.Failed, EntraxCallbackOutcome.Accepted)]
    [InlineData(EntraxSettlementOutcome.Expired, EntraxCallbackOutcome.Accepted)]
    [InlineData(EntraxSettlementOutcome.StillPending, EntraxCallbackOutcome.RetryLater)]
    [InlineData(EntraxSettlementOutcome.ProviderUnavailable, EntraxCallbackOutcome.RetryLater)]
    public async Task Solo_se_confirma_a_Entrax_lo_que_quedo_resuelto(EntraxSettlementOutcome liquidacion, EntraxCallbackOutcome esperado)
    {
        var m = await MontarAsync(liquidacion);
        var cuerpo = Cuerpo();

        (await m.Proceso.ProcessAsync(cuerpo, Firma(cuerpo), default)).Should().Be(esperado);
    }
}
