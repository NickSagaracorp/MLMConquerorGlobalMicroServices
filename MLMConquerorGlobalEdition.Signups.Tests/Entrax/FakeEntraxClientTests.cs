using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

public class FakeEntraxClientTests
{
    private static readonly DateTime Inicio = new(2026, 10, 16, 7, 0, 0, DateTimeKind.Utc);

    private static (FakeEntraxClient Fake, Mock<IDateTimeProvider> Reloj) Crear()
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(c => c.UtcNow).Returns(Inicio);
        return (new FakeEntraxClient(reloj.Object), reloj);
    }

    [Fact]
    public async Task Emite_un_QR_de_prueba_en_rublos_y_queda_pendiente()
    {
        var (fake, _) = Crear();

        var qr = (await fake.CreateQrAsync(new EntraxQrRequest(80m, "USD", "SIGNUP-o-1", null, null), default)).Value!;

        Guid.TryParse(qr.PaymentId, out _).Should().BeTrue();
        qr.ImageDataUri.Should().StartWith("data:image/png;base64,");
        qr.Link.Should().StartWith("https://");
        qr.AmountInRubles.Should().Be(80m * FakeEntraxClient.Rate);
        qr.OriginalAmount.Should().Be(80m);
        qr.ExpiresAt.Should().Be(new DateTimeOffset(Inicio).Add(FakeEntraxClient.Lifetime));

        (await fake.GetPaymentAsync(qr.PaymentId, default)).Value!.Status.Should().Be("pending");
    }

    [Fact]
    public async Task Marcar_pagado_devuelve_paid_por_el_importe_pedido()
    {
        var (fake, _) = Crear();
        var qr = (await fake.CreateQrAsync(new EntraxQrRequest(80m, "USD", "SIGNUP-o-1", null, null), default)).Value!;

        fake.MarkPaid(qr.PaymentId).Should().BeTrue();

        var estado = (await fake.GetPaymentAsync(qr.PaymentId, default)).Value!;
        estado.Status.Should().Be("paid");
        estado.Original.Should().Be(80m);
        estado.Currency.Should().Be("USD");
        estado.OrderId.Should().Be("SIGNUP-o-1");
    }

    [Fact]
    public async Task Se_puede_simular_un_pago_de_menos()
    {
        var (fake, _) = Crear();
        var qr = (await fake.CreateQrAsync(new EntraxQrRequest(80m, "USD", "SIGNUP-o-1", null, null), default)).Value!;

        fake.MarkPaid(qr.PaymentId, underpayBy: 1m);

        (await fake.GetPaymentAsync(qr.PaymentId, default)).Value!.Original.Should().Be(79m);
    }

    [Fact]
    public async Task Caduca_solo_si_nadie_lo_pago()
    {
        var (fake, reloj) = Crear();
        var qr = (await fake.CreateQrAsync(new EntraxQrRequest(80m, "USD", "SIGNUP-o-1", null, null), default)).Value!;

        reloj.Setup(c => c.UtcNow).Returns(Inicio.Add(FakeEntraxClient.Lifetime).AddSeconds(1));

        (await fake.GetPaymentAsync(qr.PaymentId, default)).Value!.Status.Should().Be("expired");
        fake.MarkPaid(qr.PaymentId);
        (await fake.GetPaymentAsync(qr.PaymentId, default)).Value!.Status.Should().Be("paid",
            "un pago tardío es justo lo que el fake tiene que poder simular.");
    }

    [Fact]
    public async Task Un_pago_desconocido_es_PAYMENT_NOT_FOUND()
    {
        var (fake, _) = Crear();
        var r = await fake.GetPaymentAsync(Guid.NewGuid().ToString(), default);

        r.Ok.Should().BeFalse();
        r.ErrorCode.Should().Be("PAYMENT_NOT_FOUND");
        fake.MarkFailed(Guid.NewGuid().ToString()).Should().BeFalse();
    }
}
