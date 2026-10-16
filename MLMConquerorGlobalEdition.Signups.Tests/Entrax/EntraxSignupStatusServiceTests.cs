using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// La pantalla pregunta cada pocos segundos; SignupAPI solo vuelve a preguntar a Entrax si hace
/// rato que no lo hace (límite de 100/min por IP). Anónimo, así que sin las dos referencias no dice nada.
/// </summary>
public class EntraxSignupStatusServiceTests
{
    private sealed record Montaje(AppDbContext Db, Mock<IEntraxSignupSettlement> Liquidacion, EntraxSignupStatusService Servicio, EntraxPayment Pago);

    private static async Task<Montaje> MontarAsync(
        EntraxPaymentStatus estado = EntraxPaymentStatus.AwaitingPayment, DateTime? consultado = null,
        DateTime? caduca = null, Action<EntraxOptions>? opciones = null)
    {
        var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, OrderStatus.Processing);
        var pago = await EscenarioEntrax.SembrarPagoAsync(db, estado, consultado: consultado, caduca: caduca);
        var liquidacion = new Mock<IEntraxSignupSettlement>();
        var servicio = new EntraxSignupStatusService(db, liquidacion.Object, EscenarioEntrax.Opciones(opciones),
            EscenarioEntrax.Reloj().Object, NullLogger<EntraxSignupStatusService>.Instance);
        return new Montaje(db, liquidacion, servicio, pago);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("otra-referencia")]
    public async Task Sin_la_referencia_del_pago_no_se_dice_nada(string? referencia)
    {
        var m = await MontarAsync();

        (await m.Servicio.GetAsync(EscenarioEntrax.OrderId, referencia, default)).Should().BeNull();
        m.Liquidacion.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Con_el_id_de_otra_alta_tampoco()
    {
        var m = await MontarAsync();

        (await m.Servicio.GetAsync("ORD-OTRO", m.Pago.Id, default)).Should().BeNull();
        m.Liquidacion.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Si_hace_rato_que_no_se_pregunta_a_Entrax_se_liquida_y_se_devuelve_lo_nuevo()
    {
        var m = await MontarAsync();
        m.Liquidacion.Setup(s => s.SettleAsync(m.Pago.Id, EntraxSettlementSource.Poll, It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                var fila = m.Db.EntraxPayments.Single();
                fila.Status = EntraxPaymentStatus.Paid;
                m.Db.SaveChanges();
            })
            .ReturnsAsync(EntraxSettlementOutcome.Activated);

        var r = await m.Servicio.GetAsync(EscenarioEntrax.OrderId, m.Pago.Id, default);

        r!.Status.Should().Be(EntraxSignupStatuses.Paid);
        r.SecondsRemaining.Should().Be(0);
    }

    [Fact]
    public async Task Si_se_pregunto_hace_poco_no_se_vuelve_a_preguntar()
    {
        var m = await MontarAsync(consultado: EscenarioEntrax.Ahora.AddSeconds(-5));

        var r = await m.Servicio.GetAsync(EscenarioEntrax.OrderId, m.Pago.Id, default);

        r!.Status.Should().Be(EntraxSignupStatuses.AwaitingPayment);
        r.SecondsRemaining.Should().Be(900);
        m.Liquidacion.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Un_pago_cerrado_no_se_vuelve_a_consultar()
    {
        var m = await MontarAsync(EntraxPaymentStatus.Expired, caduca: EscenarioEntrax.Ahora.AddMinutes(-1));

        var r = await m.Servicio.GetAsync(EscenarioEntrax.OrderId, m.Pago.Id, default);

        r!.Status.Should().Be(EntraxSignupStatuses.Expired);
        r.SecondsRemaining.Should().Be(0);
        m.Liquidacion.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Si_la_liquidacion_revienta_se_contesta_con_lo_que_hay()
    {
        var m = await MontarAsync();
        m.Liquidacion.Setup(s => s.SettleAsync(It.IsAny<string>(), It.IsAny<EntraxSettlementSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("carrera"));

        (await m.Servicio.GetAsync(EscenarioEntrax.OrderId, m.Pago.Id, default))!.Status
            .Should().Be(EntraxSignupStatuses.AwaitingPayment);
    }

    [Fact]
    public async Task Los_metodos_disponibles_dicen_en_que_paises_se_ofrece()
    {
        (await MontarAsync()).Servicio.GetPaymentOptions().EntraxCountries.Should().Equal("RU");
        (await MontarAsync(opciones: o => o.Enabled = false)).Servicio.GetPaymentOptions().EntraxCountries.Should().BeEmpty();
    }
}
