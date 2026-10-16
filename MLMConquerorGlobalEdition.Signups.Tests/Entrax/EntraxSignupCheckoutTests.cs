using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// Emitir el QR: el importe es el del servidor y en USD, el orderId es nuestro y estable, el pedido
/// queda en Processing y, si Entrax falla, vuelve a Pending para poder reintentar.
/// </summary>
public class EntraxSignupCheckoutTests
{
    private static EntraxQr Qr(
        decimal original = EscenarioEntrax.Total, string currency = "USD",
        string? image = FakeEntraxClient.PlaceholderPng, string? link = "https://qr.nspk.ru/AS100",
        DateTimeOffset? caduca = null, string? orderId = null)
        => new(EscenarioEntrax.PaymentId.ToUpperInvariant(), image, link, original * 95.5m, 95.5m, currency, original,
               caduca ?? new DateTimeOffset(EscenarioEntrax.Ahora.AddMinutes(15)), orderId ?? EscenarioEntrax.ExternalOrderId());

    private static async Task<(AppDbContext Db, Orders Order, MemberProfile Member)> AltaAsync(decimal total = EscenarioEntrax.Total)
    {
        var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, total: total);
        return (db, await db.Orders.SingleAsync(), await db.MemberProfiles.SingleAsync());
    }

    private static Mock<IEntraxClient> Cliente(EntraxCall<EntraxQr> respuesta, Action<EntraxQrRequest>? capturar = null)
    {
        var m = new Mock<IEntraxClient>();
        m.Setup(c => c.CreateQrAsync(It.IsAny<EntraxQrRequest>(), It.IsAny<CancellationToken>()))
         .Callback<EntraxQrRequest, CancellationToken>((r, _) => capturar?.Invoke(r))
         .ReturnsAsync(respuesta);
        return m;
    }

    private static EntraxSignupCheckout Checkout(AppDbContext db, Mock<IEntraxClient> cliente, ILogger<EntraxSignupCheckout>? log = null)
        => new(db, cliente.Object, EscenarioEntrax.Opciones(), log ?? NullLogger<EntraxSignupCheckout>.Instance);

    [Fact]
    public async Task Pide_el_QR_en_USD_por_el_total_del_servidor_con_un_orderId_propio()
    {
        var (db, order, member) = await AltaAsync();
        EntraxQrRequest? pedido = null;
        var cliente = Cliente(EscenarioEntrax.Ok(Qr()), r => pedido = r);

        var r = await Checkout(db, cliente).StartAsync(order, member, EscenarioEntrax.Ahora, default);

        r.IsSuccess.Should().BeTrue();
        pedido!.Amount.Should().Be(80m);
        pedido.Currency.Should().Be("USD");
        pedido.OrderId.Should().Be("SIGNUP-ORD-RU-1-1");
        pedido.CallbackUrl.Should().Be("https://alta.pruebas/api/v1/signups/entrax/callback");
        pedido.Description.Should().NotContain(EscenarioEntrax.Email, "ningún dato personal sale hacia Entrax.");

        var pago = await db.EntraxPayments.SingleAsync();
        pago.Status.Should().Be(EntraxPaymentStatus.AwaitingPayment);
        pago.ExternalPaymentId.Should().Be(EscenarioEntrax.PaymentId, "se guarda normalizado, en minúsculas.");
        pago.AmountUsd.Should().Be(80m);
        pago.AmountRub.Should().Be(80m * 95.5m);
        pago.QrLink.Should().Be("https://qr.nspk.ru/AS100");
        pago.ExpiresAt.Should().Be(EscenarioEntrax.Ahora.AddMinutes(15));
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Processing);

        r.Value!.PaymentRef.Should().Be(pago.Id);
        r.Value.QrImage.Should().Be(FakeEntraxClient.PlaceholderPng);
        r.Value.AmountRub.Should().Be(80m * 95.5m);
        r.Value.SecondsRemaining.Should().Be(900);

        var log = await db.EntraxApiCalls.SingleAsync();
        log.Operation.Should().Be("CreateQr");
        log.Success.Should().BeTrue();
        log.ExternalOrderId.Should().Be("SIGNUP-ORD-RU-1-1");
    }

    [Fact]
    public async Task El_segundo_intento_lleva_otro_orderId()
    {
        var (db, order, member) = await AltaAsync();
        await EscenarioEntrax.SembrarPagoAsync(db, EntraxPaymentStatus.Expired);
        EntraxQrRequest? pedido = null;

        await Checkout(db, Cliente(EscenarioEntrax.Ok(Qr(orderId: "SIGNUP-ORD-RU-1-2")), r => pedido = r))
            .StartAsync(order, member, EscenarioEntrax.Ahora, default);

        pedido!.OrderId.Should().Be("SIGNUP-ORD-RU-1-2");
    }

    [Fact]
    public async Task Si_Entrax_falla_el_pedido_vuelve_a_Pending_y_el_intento_queda_fallido()
    {
        var (db, order, member) = await AltaAsync();
        var cliente = Cliente(EntraxCall<EntraxQr>.Fail(400, "RATE_NOT_AVAILABLE", "No rate", 20));

        var r = await Checkout(db, cliente).StartAsync(order, member, EscenarioEntrax.Ahora, default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.Unavailable);
        var pago = await db.EntraxPayments.SingleAsync();
        pago.Status.Should().Be(EntraxPaymentStatus.Failed);
        pago.FailureCode.Should().Be("RATE_NOT_AVAILABLE");
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
        var log = await db.EntraxApiCalls.SingleAsync();
        log.Success.Should().BeFalse();
        log.ErrorCode.Should().Be("RATE_NOT_AVAILABLE");
        log.HttpStatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Un_QR_por_otro_importe_no_se_muestra_y_se_avisa_como_critico()
    {
        var (db, order, member) = await AltaAsync();
        var log = new RegistroDePrueba<EntraxSignupCheckout>();

        var r = await Checkout(db, Cliente(EscenarioEntrax.Ok(Qr(original: 79m))), log)
            .StartAsync(order, member, EscenarioEntrax.Ahora, default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.Unavailable);
        (await db.EntraxPayments.SingleAsync()).FailureCode.Should().Be("QR_AMOUNT_MISMATCH");
        log.Entradas.Should().Contain(e => e.Nivel == LogLevel.Critical);
    }

    [Fact]
    public async Task Un_QR_de_otro_orderId_no_se_muestra_y_el_pedido_vuelve_a_Pending()
    {
        var (db, order, member) = await AltaAsync();
        var log = new RegistroDePrueba<EntraxSignupCheckout>();

        var r = await Checkout(db, Cliente(EscenarioEntrax.Ok(Qr(orderId: "SIGNUP-OTRO-1"))), log)
            .StartAsync(order, member, EscenarioEntrax.Ahora, default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.Unavailable);
        var pago = await db.EntraxPayments.SingleAsync();
        pago.Status.Should().Be(EntraxPaymentStatus.Failed);
        pago.FailureCode.Should().Be("PROVIDER_MISMATCH");
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
        log.Entradas.Should().Contain(e => e.Nivel == LogLevel.Critical);
    }

    [Fact]
    public async Task Un_intento_borrado_tambien_cuenta_para_no_repetir_el_orderId()
    {
        var (db, order, member) = await AltaAsync();
        var borrado = await EscenarioEntrax.SembrarPagoAsync(db, EntraxPaymentStatus.Failed, paymentId: null);
        borrado.IsDeleted = true;
        await db.SaveChangesAsync();
        EntraxQrRequest? pedido = null;

        await Checkout(db, Cliente(EscenarioEntrax.Ok(Qr(orderId: "SIGNUP-ORD-RU-1-2")), r => pedido = r))
            .StartAsync(order, member, EscenarioEntrax.Ahora, default);

        pedido!.OrderId.Should().Be("SIGNUP-ORD-RU-1-2");
    }

    [Fact]
    public async Task Un_QR_en_rublos_tampoco_se_muestra()
    {
        var (db, order, member) = await AltaAsync();

        var r = await Checkout(db, Cliente(EscenarioEntrax.Ok(Qr(currency: "RUB"))))
            .StartAsync(order, member, EscenarioEntrax.Ahora, default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.Unavailable);
        (await db.EntraxPayments.SingleAsync()).FailureCode.Should().Be("QR_AMOUNT_MISMATCH");
    }

    [Fact]
    public async Task Un_QR_sin_imagen_ni_enlace_usables_no_se_muestra()
    {
        var (db, order, member) = await AltaAsync();

        var r = await Checkout(db, Cliente(EscenarioEntrax.Ok(Qr(image: "data:image/svg+xml;base64,PHN2Zz4=", link: "javascript:alert(1)"))))
            .StartAsync(order, member, EscenarioEntrax.Ahora, default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.Unavailable);
        (await db.EntraxPayments.SingleAsync()).FailureCode.Should().Be("QR_UNUSABLE");
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Sin_nada_que_pagar_no_llama_a_Entrax()
    {
        var (db, order, member) = await AltaAsync(total: 0m);
        var cliente = Cliente(EscenarioEntrax.Ok(Qr()));

        var r = await Checkout(db, cliente).StartAsync(order, member, EscenarioEntrax.Ahora, default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.NothingToPay);
        cliente.Verify(c => c.CreateQrAsync(It.IsAny<EntraxQrRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        (await db.EntraxPayments.CountAsync()).Should().Be(0);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task La_caducidad_que_llega_en_UTC_se_guarda_en_hora_de_servidor()
    {
        var (db, order, member) = await AltaAsync();
        var utc = new DateTimeOffset(2026, 10, 16, 7, 15, 0, TimeSpan.Zero);

        await Checkout(db, Cliente(EscenarioEntrax.Ok(Qr(caduca: utc)))).StartAsync(order, member, EscenarioEntrax.Ahora, default);

        (await db.EntraxPayments.SingleAsync()).ExpiresAt.Should().Be(utc.LocalDateTime);
    }

    [Fact]
    public void Se_ofrece_segun_las_opciones()
    {
        using var db = InMemoryDbHelper.Create();
        var checkout = Checkout(db, new Mock<IEntraxClient>());

        checkout.IsOfferedFor("RU").Should().BeTrue();
        checkout.IsOfferedFor("US").Should().BeFalse();
    }
}
