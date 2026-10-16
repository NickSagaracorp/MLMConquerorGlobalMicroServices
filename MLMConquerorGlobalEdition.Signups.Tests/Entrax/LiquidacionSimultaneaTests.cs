using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// El webhook y la consulta de la pantalla (o el barrido) liquidan EL MISMO pago a la vez. El que
/// llega segundo no puede marcar a revisar —como doble cobro— un pago legítimo que el primero ya
/// liquidó: reclama el pago de forma atómica antes de nada (si ya está Paid, AlreadyResolved) y, si
/// aun así pierde en el pedido, relee el pago y, si está Paid, también es AlreadyResolved.
///
/// "El otro camino" escribe por otro contexto sobre la misma base en memoria, como haría otra
/// petición. La carrera real (dos transacciones en SQL Server) está en SqlServerEntraxSettlementTests.
/// </summary>
public class LiquidacionSimultaneaTests
{
    private sealed record Montaje(
        string Base, AppDbContext Db, Mock<IEntraxClient> Cliente, Mock<ISignupActivationService> Activacion,
        RegistroDePrueba<EntraxSignupSettlement> Log, EntraxSignupSettlement Liquidacion, string PagoId);

    private static async Task<Montaje> MontarAsync()
    {
        var nombre = Guid.NewGuid().ToString();
        var db = InMemoryDbHelper.Create(nombre);
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        var fila = await EscenarioEntrax.SembrarPagoAsync(db);

        var cliente = new Mock<IEntraxClient>();
        cliente.Setup(c => c.GetPaymentAsync(EscenarioEntrax.PaymentId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(EscenarioEntrax.Ok(EscenarioEntrax.Remoto("paid")));

        var activacion = new Mock<ISignupActivationService>();
        var log = new RegistroDePrueba<EntraxSignupSettlement>();
        var liquidacion = new EntraxSignupSettlement(db, cliente.Object, activacion.Object,
            EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()).Object, EscenarioEntrax.Reloj().Object, log);
        return new Montaje(nombre, db, cliente, activacion, log, liquidacion, fila.Id);
    }

    /// <summary>Lo que deja el camino que ganó: el pago Paid y consumido, el pedido Completed.</summary>
    private static async Task OtroCaminoLiquidoAsync(string nombre)
    {
        await using var otro = InMemoryDbHelper.Create(nombre);
        var pago = await otro.EntraxPayments.SingleAsync();
        pago.Status   = EntraxPaymentStatus.Paid;
        pago.Consumed = true;
        (await otro.Orders.SingleAsync()).Status = OrderStatus.Completed;
        await otro.SaveChangesAsync();
    }

    private static async Task<EntraxPayment> PagoEnLaBaseAsync(string nombre)
    {
        await using var otro = InMemoryDbHelper.Create(nombre);
        return await otro.EntraxPayments.AsNoTracking().SingleAsync();
    }

    [Fact]
    public async Task Si_el_otro_camino_liquido_mientras_se_preguntaba_a_Entrax_es_AlreadyResolved_sin_activar()
    {
        var m = await MontarAsync();
        m.Cliente.Setup(c => c.GetPaymentAsync(EscenarioEntrax.PaymentId, It.IsAny<CancellationToken>()))
                 .Returns(async () =>
                 {
                     await OtroCaminoLiquidoAsync(m.Base);
                     return EscenarioEntrax.Ok(EscenarioEntrax.Remoto("paid"));
                 });

        var r = await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Poll, default);

        r.Should().Be(EntraxSettlementOutcome.AlreadyResolved);
        m.Activacion.VerifyNoOtherCalls();
        (await PagoEnLaBaseAsync(m.Base)).Status.Should().Be(EntraxPaymentStatus.Paid);
        m.Log.Entradas.Should().NotContain(e => e.Nivel == LogLevel.Critical);
    }

    [Theory]
    [InlineData(SignupOrderClaim.AlreadySettledCode)]
    [InlineData("SUBSCRIPTION_NOT_FOUND")]
    public async Task Si_pierde_en_el_pedido_porque_el_otro_camino_liquido_ESTE_pago_no_es_doble_cobro(string codigo)
    {
        var m = await MontarAsync();
        m.Activacion.Setup(a => a.ActivatePendingSignupAsync(It.IsAny<Orders>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                    .Returns(async () =>
                    {
                        await OtroCaminoLiquidoAsync(m.Base);
                        return Result<MemberProfile>.Failure(codigo, "perdió");
                    });

        var r = await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default);

        r.Should().Be(EntraxSettlementOutcome.AlreadyResolved);
        var pago = await PagoEnLaBaseAsync(m.Base);
        pago.Status.Should().Be(EntraxPaymentStatus.Paid, "un pago legítimo ya liquidado no pasa a revisar.");
        pago.FailureCode.Should().BeNull();
        m.Log.Entradas.Should().NotContain(e => e.Nivel == LogLevel.Critical);
    }

    [Fact]
    public async Task Una_excepcion_dentro_de_la_transaccion_no_deja_nada_marcado_en_el_contexto()
    {
        var m = await MontarAsync();
        m.Activacion.Setup(a => a.ActivatePendingSignupAsync(It.IsAny<Orders>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new InvalidOperationException("se cayó la red"));

        var accion = () => m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Sweep, default);

        await accion.Should().ThrowAsync<InvalidOperationException>();
        m.Db.ChangeTracker.Entries().Should().BeEmpty(
            "si el mismo contexto se reutiliza, no puede guardar Paid, Consumed y el PaymentHistory sin haber activado.");
        await m.Db.SaveChangesAsync();
        (await PagoEnLaBaseAsync(m.Base)).Status.Should().Be(EntraxPaymentStatus.AwaitingPayment);
    }
}
