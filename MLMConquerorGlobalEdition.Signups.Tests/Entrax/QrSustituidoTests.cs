using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SignupAPI.Jobs;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// Cuando el alta ya se activó (con otro QR, cripto o tarjeta), un QR que sigue vivo en Entrax no puede
/// seguir pareciendo cobrable en la pantalla: el estado dice Superseded con 0 segundos y la pantalla
/// lo oculta. Al activar con un QR, los demás que esperaban del mismo pedido pasan a Superseded; el
/// barrido los sigue mirando mientras puedan pagarse, y si alguien los paga es un doble cobro a revisar.
/// </summary>
public class QrSustituidoTests
{
    [Fact]
    public async Task Si_el_alta_ya_esta_completada_la_pantalla_de_otro_QR_lo_da_por_sustituido_sin_preguntar_a_Entrax()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Completed);
        var pago = await EscenarioEntrax.SembrarPagoAsync(db);
        var liquidacion = new Mock<IEntraxSignupSettlement>();

        var r = await new EntraxSignupStatusService(db, liquidacion.Object, EscenarioEntrax.Opciones(),
                EscenarioEntrax.Reloj().Object, NullLogger<EntraxSignupStatusService>.Instance)
            .GetAsync(EscenarioEntrax.OrderId, pago.Id, default);

        r!.Status.Should().Be(EntraxSignupStatuses.Superseded);
        r.SecondsRemaining.Should().Be(0, "la pantalla oculta el QR.");
        liquidacion.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Al_activar_con_un_QR_los_demas_que_esperaban_del_mismo_pedido_quedan_sustituidos()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        var pagado = await EscenarioEntrax.SembrarPagoAsync(db);
        var otro = await EscenarioEntrax.SembrarPagoAsync(db, paymentId: Guid.NewGuid().ToString(), intento: 2);
        var cliente = new Mock<IEntraxClient>();
        cliente.Setup(c => c.GetPaymentAsync(EscenarioEntrax.PaymentId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(EscenarioEntrax.Ok(EscenarioEntrax.Remoto("paid")));

        (await new EntraxSignupSettlement(db, cliente.Object, EscenarioEntrax.Activacion(db),
                EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()).Object, EscenarioEntrax.Reloj().Object,
                NullLogger<EntraxSignupSettlement>.Instance)
            .SettleAsync(pagado.Id, EntraxSettlementSource.Webhook, default)).Should().Be(EntraxSettlementOutcome.Activated);

        (await db.EntraxPayments.AsNoTracking().SingleAsync(p => p.Id == otro.Id)).Status
            .Should().Be(EntraxPaymentStatus.Superseded);
    }

    [Fact]
    public async Task Un_QR_sustituido_que_alguien_paga_es_un_doble_cobro_a_revisar()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Completed);
        var pago = await EscenarioEntrax.SembrarPagoAsync(db, EntraxPaymentStatus.Superseded);
        var cliente = new Mock<IEntraxClient>();
        cliente.Setup(c => c.GetPaymentAsync(EscenarioEntrax.PaymentId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(EscenarioEntrax.Ok(EscenarioEntrax.Remoto("paid")));

        var r = await new EntraxSignupSettlement(db, cliente.Object, EscenarioEntrax.Activacion(db),
                EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()).Object, EscenarioEntrax.Reloj().Object,
                NullLogger<EntraxSignupSettlement>.Instance)
            .SettleAsync(pago.Id, EntraxSettlementSource.Sweep, default);

        r.Should().Be(EntraxSettlementOutcome.ToReview);
        (await db.EntraxPayments.AsNoTracking().SingleAsync()).FailureCode.Should().Be(SignupOrderClaim.AlreadySettledCode);
    }

    [Fact]
    public async Task El_barrido_sigue_mirando_los_sustituidos_mientras_puedan_pagarse()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Completed);
        var vivo = await EscenarioEntrax.SembrarPagoAsync(db, EntraxPaymentStatus.Superseded,
            paymentId: Guid.NewGuid().ToString(), intento: 1, caduca: EscenarioEntrax.Ahora.AddMinutes(10));
        var tardio = await EscenarioEntrax.SembrarPagoAsync(db, EntraxPaymentStatus.Superseded,
            paymentId: Guid.NewGuid().ToString(), intento: 2, caduca: EscenarioEntrax.Ahora.AddMinutes(-30));
        await EscenarioEntrax.SembrarPagoAsync(db, EntraxPaymentStatus.Superseded,
            paymentId: Guid.NewGuid().ToString(), intento: 3, caduca: EscenarioEntrax.Ahora.AddHours(-5));
        var liquidacion = new Mock<IEntraxSignupSettlement>();
        var services = new ServiceCollection();
        services.AddScoped(_ => liquidacion.Object);

        await new EntraxSignupSweepJob(db, services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            EscenarioEntrax.Opciones(), EscenarioEntrax.Reloj().Object, NullLogger<EntraxSignupSweepJob>.Instance).ExecuteAsync();

        liquidacion.Verify(s => s.SettleAsync(vivo.Id, EntraxSettlementSource.Sweep, It.IsAny<CancellationToken>()), Times.Once);
        liquidacion.Verify(s => s.SettleAsync(tardio.Id, EntraxSettlementSource.Sweep, It.IsAny<CancellationToken>()), Times.Once);
        liquidacion.VerifyNoOtherCalls();
    }
}
