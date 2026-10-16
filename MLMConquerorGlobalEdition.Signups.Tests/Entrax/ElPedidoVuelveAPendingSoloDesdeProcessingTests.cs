using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// Devolver el pedido a Pending (un QR fallido o caducado) es una transición condicional desde
/// Processing, nunca una escritura a ciegas: si entre tanto un pago tardío activó el pedido, se queda
/// Completed. Si no, el alta quedaría con el pedido Pending y el miembro activo.
/// </summary>
public class ElPedidoVuelveAPendingSoloDesdeProcessingTests
{
    /// <summary>Otro camino ya completó el pedido: la vuelta a Pending no encuentra Processing.</summary>
    private sealed class PedidoYaCompletado : ISignupOrderClaim
    {
        public int Liberaciones { get; private set; }

        public Task<bool> TryCompleteAsync(Orders order, DateTime now, string actor, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> TryCompleteFromPendingAsync(Orders order, DateTime now, string actor, CancellationToken ct) => Task.FromResult(false);

        public Task<bool> TryReleaseAsync(Orders order, DateTime now, string actor, CancellationToken ct)
        {
            Liberaciones++;
            return Task.FromResult(false);
        }
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("expired")]
    public async Task Un_QR_que_falla_o_caduca_no_devuelve_a_Pending_un_pedido_que_ya_no_esta_en_Processing(string remoto)
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        var pago = await EscenarioEntrax.SembrarPagoAsync(db);
        var cliente = new Mock<IEntraxClient>();
        cliente.Setup(c => c.GetPaymentAsync(EscenarioEntrax.PaymentId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(EscenarioEntrax.Ok(EscenarioEntrax.Remoto(remoto)));
        var reclamo = new PedidoYaCompletado();
        var liquidacion = new EntraxSignupSettlement(db, cliente.Object, EscenarioEntrax.Activacion(db),
            EscenarioEntrax.Usuarios(EscenarioEntrax.UsuarioInactivo()).Object, EscenarioEntrax.Reloj().Object,
            NullLogger<EntraxSignupSettlement>.Instance, reclamo);

        await liquidacion.SettleAsync(pago.Id, EntraxSettlementSource.Sweep, default);

        reclamo.Liberaciones.Should().Be(1);
        (await db.Orders.AsNoTracking().SingleAsync()).Status.Should().NotBe(OrderStatus.Pending);
    }

    [Fact]
    public async Task Si_Entrax_no_emite_el_QR_tampoco_se_devuelve_a_ciegas()
    {
        await using var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db);
        var order = await db.Orders.SingleAsync();
        var member = await db.MemberProfiles.SingleAsync();
        var cliente = new Mock<IEntraxClient>();
        cliente.Setup(c => c.CreateQrAsync(It.IsAny<EntraxQrRequest>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(EntraxCall<EntraxQr>.Fail(400, "RATE_NOT_AVAILABLE", null, 1));
        var reclamo = new PedidoYaCompletado();

        var r = await new EntraxSignupCheckout(db, cliente.Object, EscenarioEntrax.Opciones(),
                NullLogger<EntraxSignupCheckout>.Instance, reclamo)
            .StartAsync(order, member, EscenarioEntrax.Ahora, default);

        r.ErrorCode.Should().Be(EntraxSignupErrors.Unavailable);
        reclamo.Liberaciones.Should().Be(1);
        (await db.Orders.AsNoTracking().SingleAsync()).Status.Should().Be(OrderStatus.Processing,
            "la vuelta a Pending es condicional; aquí la decide el doble que dice que el pedido ya no está en Processing.");
    }
}
