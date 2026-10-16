using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Jobs;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// Un pedido no puede quedarse en Processing para siempre porque Entrax no conteste (caído, sin
/// credenciales) o siga diciendo "pending" de un QR ya caducado: el barrido, pasado ExpiresAt más
/// Entrax:LocalExpiryGraceMinutes, lo cierra en local como Expired y el pedido vuelve (condicional) a
/// Pending. Si luego resulta pagado, el barrido de pagos tardíos lo sigue mirando.
/// </summary>
public class CaducidadLocalTests
{
    private sealed record Montaje(AppDbContext Db, Mock<IEntraxClient> Cliente, EntraxSignupSettlement Liquidacion, string PagoId);

    private static async Task<Montaje> MontarAsync(DateTime caduca, EntraxCall<EntraxPaymentState> respuesta)
    {
        var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        var fila = await EscenarioEntrax.SembrarPagoAsync(db, caduca: caduca);
        var cliente = new Mock<IEntraxClient>();
        cliente.Setup(c => c.GetPaymentAsync(EscenarioEntrax.PaymentId, It.IsAny<CancellationToken>())).ReturnsAsync(respuesta);
        var liquidacion = new EntraxSignupSettlement(db, cliente.Object, EscenarioEntrax.Activacion(db),
            EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()).Object, EscenarioEntrax.Reloj().Object,
            NullLogger<EntraxSignupSettlement>.Instance, options: EscenarioEntrax.Opciones(o => o.LocalExpiryGraceMinutes = 10));
        return new Montaje(db, cliente, liquidacion, fila.Id);
    }

    private static EntraxCall<EntraxPaymentState> Caido(string codigo) => EntraxCall<EntraxPaymentState>.Fail(0, codigo, null, 1);

    public static TheoryData<string, EntraxCall<EntraxPaymentState>> SinRespuestaUtil => new()
    {
        { "Entrax caído",            Caido(EntraxErrorCodes.Transport) },
        { "sin credenciales",        Caido(EntraxErrorCodes.NotConfigured) },
        { "sigue diciendo pending",  EscenarioEntrax.Ok(EscenarioEntrax.Remoto("pending")) }
    };

    [Theory]
    [MemberData(nameof(SinRespuestaUtil))]
    public async Task Pasada_la_caducidad_y_el_margen_el_barrido_lo_cierra_en_local_y_libera_el_pedido(
        string caso, EntraxCall<EntraxPaymentState> respuesta)
    {
        var m = await MontarAsync(EscenarioEntrax.Ahora.AddMinutes(-11), respuesta);

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Sweep, default))
            .Should().Be(EntraxSettlementOutcome.Expired, caso);

        var pago = await m.Db.EntraxPayments.AsNoTracking().SingleAsync();
        pago.Status.Should().Be(EntraxPaymentStatus.Expired);
        pago.FailureCode.Should().Be("LOCAL_EXPIRY");
        (await m.Db.Orders.AsNoTracking().SingleAsync()).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Si_luego_resulta_pagado_activa_y_el_codigo_de_la_caducidad_local_pasa_a_las_notas()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Pending);
        var pago = await EscenarioEntrax.SembrarPagoAsync(db, EntraxPaymentStatus.Expired, caduca: EscenarioEntrax.Ahora.AddMinutes(-30));
        pago.FailureCode = "LOCAL_EXPIRY";
        await db.SaveChangesAsync();
        var cliente = new Mock<IEntraxClient>();
        cliente.Setup(c => c.GetPaymentAsync(EscenarioEntrax.PaymentId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(EscenarioEntrax.Ok(EscenarioEntrax.Remoto("paid")));

        (await new EntraxSignupSettlement(db, cliente.Object, EscenarioEntrax.Activacion(db),
                EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()).Object, EscenarioEntrax.Reloj().Object,
                NullLogger<EntraxSignupSettlement>.Instance)
            .SettleAsync(pago.Id, EntraxSettlementSource.Sweep, default)).Should().Be(EntraxSettlementOutcome.Activated);

        var fila = await db.EntraxPayments.AsNoTracking().SingleAsync();
        fila.Status.Should().Be(EntraxPaymentStatus.Paid);
        fila.FailureCode.Should().BeNull("un pago Paid no lleva código de fallo.");
        fila.Notes.Should().Contain("LOCAL_EXPIRY");
    }

    [Fact]
    public async Task Dentro_del_margen_se_sigue_esperando()
    {
        var m = await MontarAsync(EscenarioEntrax.Ahora.AddMinutes(-5), Caido(EntraxErrorCodes.Transport));

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Sweep, default))
            .Should().Be(EntraxSettlementOutcome.ProviderUnavailable);
        (await m.Db.EntraxPayments.AsNoTracking().SingleAsync()).Status.Should().Be(EntraxPaymentStatus.AwaitingPayment);
    }

    [Fact]
    public async Task Solo_lo_cierra_el_barrido_no_la_pantalla()
    {
        var m = await MontarAsync(EscenarioEntrax.Ahora.AddMinutes(-30), Caido(EntraxErrorCodes.Transport));

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Poll, default))
            .Should().Be(EntraxSettlementOutcome.ProviderUnavailable);
    }

    [Fact]
    public async Task Con_Entrax_apagado_y_pagos_esperando_el_barrido_avisa_como_Critical()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        await EscenarioEntrax.SembrarPagoAsync(db);
        var services = new ServiceCollection();
        services.AddScoped(_ => Mock.Of<IEntraxSignupSettlement>());
        var log = new RegistroDePrueba<EntraxSignupSweepJob>();

        await new EntraxSignupSweepJob(db, services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
                EscenarioEntrax.Opciones(o => o.Enabled = false), EscenarioEntrax.Reloj().Object, log)
            .ExecuteAsync();

        log.Entradas.Should().Contain(e => e.Nivel == LogLevel.Critical);
    }
}
