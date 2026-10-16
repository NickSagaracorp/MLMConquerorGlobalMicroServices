using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// Un pago ya liquidado repara la cuenta de Identity solo si el proceso cayó entre activar y activar el
/// usuario: pago Paid, miembro Active y pedido Completed. Un Re-check o un callback repetido no pueden
/// reactivar una cuenta que un administrador desactivó (Terminada no es reactivable), ni un pago a revisar.
/// </summary>
public class ReparacionDelUsuarioTests
{
    private sealed record Montaje(string Base, AppDbContext Db, Mock<IEntraxClient> Cliente, ApplicationUser Usuario,
        EntraxSignupSettlement Liquidacion, string PagoId);

    private static async Task<Montaje> MontarAsync(
        EntraxPaymentStatus pago, OrderStatus pedido, MemberAccountStatus miembro)
    {
        var nombre = Guid.NewGuid().ToString();
        var db = InMemoryDbHelper.Create(nombre);
        await EscenarioEntrax.SembrarAltaAsync(db, pedido);
        (await db.MemberProfiles.SingleAsync()).Status = miembro;
        await db.SaveChangesAsync();
        var fila = await EscenarioEntrax.SembrarPagoAsync(db, pago);

        var cliente = new Mock<IEntraxClient>();
        cliente.Setup(c => c.GetPaymentAsync(EscenarioEntrax.PaymentId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(EscenarioEntrax.Ok(EscenarioEntrax.Remoto("paid")));
        var usuario = EscenarioEntrax.UsuarioInactivo();
        var liquidacion = new EntraxSignupSettlement(db, cliente.Object, EscenarioEntrax.Activacion(db),
            EscenarioEntrax.Usuarios(usuario).Object, EscenarioEntrax.Reloj().Object, NullLogger<EntraxSignupSettlement>.Instance);
        return new Montaje(nombre, db, cliente, usuario, liquidacion, fila.Id);
    }

    [Fact]
    public async Task Pagado_activo_y_completado_repara_la_cuenta_que_quedo_inactiva()
    {
        var m = await MontarAsync(EntraxPaymentStatus.Paid, OrderStatus.Completed, MemberAccountStatus.Active);

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Admin, default))
            .Should().Be(EntraxSettlementOutcome.AlreadyResolved);
        m.Usuario.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(MemberAccountStatus.Inactive)]
    [InlineData(MemberAccountStatus.Terminated)]
    public async Task Un_Re_check_no_reactiva_una_cuenta_que_un_administrador_desactivo(MemberAccountStatus estado)
    {
        var m = await MontarAsync(EntraxPaymentStatus.Paid, OrderStatus.Completed, estado);

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Admin, default))
            .Should().Be(EntraxSettlementOutcome.AlreadyResolved);
        m.Usuario.IsActive.Should().BeFalse("Terminada no es reactivable, y tampoco por un callback repetido.");
    }

    [Fact]
    public async Task Con_el_pedido_sin_completar_tampoco_se_activa_la_cuenta()
    {
        var m = await MontarAsync(EntraxPaymentStatus.Paid, OrderStatus.Processing, MemberAccountStatus.Active);

        await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default);

        m.Usuario.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Si_el_reclamo_pierde_porque_el_pago_quedo_a_revisar_no_se_activa_nada()
    {
        var m = await MontarAsync(EntraxPaymentStatus.AwaitingPayment, OrderStatus.Completed, MemberAccountStatus.Active);
        // El pedido sigue Processing para la lectura de la liquidación; el otro camino lo deja a revisar.
        (await m.Db.Orders.SingleAsync()).Status = OrderStatus.Processing;
        await m.Db.SaveChangesAsync();
        m.Cliente.Setup(c => c.GetPaymentAsync(EscenarioEntrax.PaymentId, It.IsAny<CancellationToken>()))
                 .Returns(async () =>
                 {
                     await using var otro = InMemoryDbHelper.Create(m.Base);
                     (await otro.EntraxPayments.SingleAsync()).Status = EntraxPaymentStatus.ToReview;
                     (await otro.Orders.SingleAsync()).Status = OrderStatus.Completed;
                     await otro.SaveChangesAsync();
                     return EscenarioEntrax.Ok(EscenarioEntrax.Remoto("paid"));
                 });

        var r = await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Poll, default);

        r.Should().Be(EntraxSettlementOutcome.AlreadyResolved);
        m.Usuario.IsActive.Should().BeFalse("un pago a revisar no da acceso.");
    }
}
