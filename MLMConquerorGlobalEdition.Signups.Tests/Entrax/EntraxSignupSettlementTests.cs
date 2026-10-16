using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// El único camino que activa un alta pagada con Entrax. Siempre re-consulta GET /payment (no se fía
/// de quien avisa), exige paid + USD + importe suficiente, y un pago activa un alta una sola vez.
/// </summary>
public class EntraxSignupSettlementTests
{
    private sealed record Montaje(
        AppDbContext Db, Mock<IEntraxClient> Cliente, ApplicationUser Usuario,
        RegistroDePrueba<EntraxSignupSettlement> Log, EntraxSignupSettlement Liquidacion, string PagoId);

    private static async Task<Montaje> MontarAsync(
        OrderStatus pedido = OrderStatus.Processing,
        EntraxPaymentStatus pago = EntraxPaymentStatus.AwaitingPayment,
        EntraxPaymentState? remoto = null,
        Func<AppDbContext, ISignupActivationService>? activacion = null,
        string? paymentId = EscenarioEntrax.PaymentId,
        DateTime? creado = null)
    {
        var db = InMemoryDbHelper.Create();
        await EscenarioEntrax.SembrarAltaAsync(db, pedido);
        var fila = await EscenarioEntrax.SembrarPagoAsync(db, pago, paymentId: paymentId, creado: creado);

        var cliente = new Mock<IEntraxClient>();
        cliente.Setup(c => c.GetPaymentAsync(EscenarioEntrax.PaymentId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(EscenarioEntrax.Ok(remoto ?? EscenarioEntrax.Remoto("paid")));

        var usuario = EscenarioEntrax.UsuarioInactivo();
        var log = new RegistroDePrueba<EntraxSignupSettlement>();
        var liquidacion = new EntraxSignupSettlement(db, cliente.Object,
            (activacion ?? EscenarioEntrax.Activacion)(db), EscenarioEntrax.Usuarios(usuario).Object,
            EscenarioEntrax.Reloj().Object, log);

        return new Montaje(db, cliente, usuario, log, liquidacion, fila.Id);
    }

    private static Task<EntraxPayment> Fila(Montaje m) => m.Db.EntraxPayments.SingleAsync(p => p.Id == m.PagoId);

    // ── Pagado y cuadrado ────────────────────────────────────────────────────────

    [Fact]
    public async Task Pagado_y_cuadrado_activa_el_alta_registra_el_pago_y_da_acceso()
    {
        var m = await MontarAsync();

        var r = await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default);

        r.Should().Be(EntraxSettlementOutcome.Activated);
        var pago = await Fila(m);
        pago.Status.Should().Be(EntraxPaymentStatus.Paid);
        pago.Consumed.Should().BeTrue();
        pago.ActivatedAt.Should().Be(EscenarioEntrax.Ahora);
        pago.PaidAmountUsd.Should().Be(80m);
        pago.PaidAt.Should().Be(EscenarioEntrax.Ahora.AddMinutes(5));

        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Completed);
        (await m.Db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Active);

        var cobro = await m.Db.PaymentHistories.SingleAsync();
        cobro.GatewayName.Should().Be("Entrax");
        cobro.GatewayTransactionId.Should().Be(EscenarioEntrax.PaymentId);
        cobro.Amount.Should().Be(80m);
        cobro.OrderId.Should().Be(EscenarioEntrax.OrderId);
        cobro.TransactionStatus.Should().Be(PaymentHistoryTransactionStatus.Captured);

        m.Usuario.IsActive.Should().BeTrue("tras cobrar, la persona inicia sesión con su contraseña.");
        (await m.Db.EntraxApiCalls.SingleAsync()).Operation.Should().Be("GetPayment");
    }

    [Fact]
    public async Task Un_segundo_aviso_del_mismo_pago_no_vuelve_a_activar_ni_a_cobrar()
    {
        var m = await MontarAsync();

        await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default);
        var segunda = await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Poll, default);

        segunda.Should().Be(EntraxSettlementOutcome.AlreadyResolved);
        (await m.Db.PaymentHistories.CountAsync()).Should().Be(1);
        m.Cliente.Verify(c => c.GetPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once,
            "un pago ya liquidado no se vuelve a consultar.");
    }

    [Fact]
    public async Task Un_pago_ya_liquidado_repara_el_usuario_si_quedo_inactivo()
    {
        var m = await MontarAsync(pago: EntraxPaymentStatus.Paid);

        var r = await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Admin, default);

        r.Should().Be(EntraxSettlementOutcome.AlreadyResolved);
        m.Usuario.IsActive.Should().BeTrue();
        m.Cliente.Verify(c => c.GetPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Una_carrera_al_activar_se_resuelve_releyendo()
    {
        var veces = 0;
        var m = await MontarAsync(activacion: db =>
        {
            var real = EscenarioEntrax.Activacion(db);
            var act = new Mock<ISignupActivationService>();
            act.Setup(a => a.ActivatePendingSignupAsync(It.IsAny<Orders>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
               .Returns((Orders o, DateTime n, CancellationToken c) =>
                   ++veces == 1 ? throw new DbUpdateConcurrencyException("carrera") : real.ActivatePendingSignupAsync(o, n, c));
            return act.Object;
        });

        var r = await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Sweep, default);

        r.Should().Be(EntraxSettlementOutcome.Activated);
        veces.Should().Be(2);
        (await m.Db.PaymentHistories.CountAsync()).Should().Be(1, "lo del intento perdido se descartó.");
    }

    // ── Pagado pero no se aplica: a revisar ──────────────────────────────────────

    [Fact]
    public async Task Pagado_de_menos_queda_a_revisar_sin_activar_nada()
    {
        var m = await MontarAsync(remoto: EscenarioEntrax.Remoto("paid", original: 79m));

        var r = await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default);

        r.Should().Be(EntraxSettlementOutcome.ToReview);
        var pago = await Fila(m);
        pago.Status.Should().Be(EntraxPaymentStatus.ToReview);
        pago.FailureCode.Should().Be("AMOUNT_TOO_LOW");
        pago.PaidAmountUsd.Should().Be(79m);
        pago.Consumed.Should().BeFalse();
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Processing);
        (await m.Db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Pending);
        (await m.Db.PaymentHistories.CountAsync()).Should().Be(0);
        m.Usuario.IsActive.Should().BeFalse();
        m.Log.Entradas.Should().Contain(e => e.Nivel == LogLevel.Critical);
    }

    [Fact]
    public async Task Se_compara_tambien_con_el_total_actual_del_pedido()
    {
        var m = await MontarAsync();
        (await m.Db.Orders.SingleAsync()).TotalAmount = 100m;
        await m.Db.SaveChangesAsync();

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default))
            .Should().Be(EntraxSettlementOutcome.ToReview);
        (await Fila(m)).FailureCode.Should().Be("AMOUNT_TOO_LOW");
    }

    [Fact]
    public async Task Pagado_en_otra_moneda_queda_a_revisar()
    {
        var m = await MontarAsync(remoto: EscenarioEntrax.Remoto("paid", currency: "RUB"));

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default))
            .Should().Be(EntraxSettlementOutcome.ToReview);
        (await Fila(m)).FailureCode.Should().Be("CURRENCY_MISMATCH");
    }

    [Fact]
    public async Task Pagado_cuando_el_alta_ya_se_activo_por_otro_medio_es_un_doble_cobro()
    {
        var m = await MontarAsync(pedido: OrderStatus.Completed);

        var r = await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default);

        r.Should().Be(EntraxSettlementOutcome.ToReview);
        (await Fila(m)).FailureCode.Should().Be("ORDER_ALREADY_SETTLED");
        (await m.Db.PaymentHistories.CountAsync()).Should().Be(0);
        m.Log.Entradas.Should().Contain(e => e.Nivel == LogLevel.Critical);
    }

    [Fact]
    public async Task Un_pago_que_Entrax_atribuye_a_otro_pedido_no_se_aplica()
    {
        var m = await MontarAsync(remoto: EscenarioEntrax.Remoto("paid", orderId: "SIGNUP-OTRO-1"));

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default))
            .Should().Be(EntraxSettlementOutcome.ToReview);
        (await Fila(m)).FailureCode.Should().Be("PROVIDER_MISMATCH");
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Processing);
    }

    [Fact]
    public async Task Si_otro_pago_activo_el_pedido_en_paralelo_es_un_doble_cobro_sin_activar_nada()
    {
        // Otro QR del mismo pedido (o la cripto) pasó el pedido a Completed entre la lectura y la
        // activación: la transición atómica del pedido devuelve false.
        var m = await MontarAsync(activacion: db => new SignupActivationService(db, Mock.Of<ISponsorBonusService>(),
            Mock.Of<IFastStartBonusService>(), Mock.Of<IRecurringBillingEnrollmentService>(), new CarreraPerdida()));

        var r = await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default);

        r.Should().Be(EntraxSettlementOutcome.ToReview);
        var pago = await Fila(m);
        pago.Status.Should().Be(EntraxPaymentStatus.ToReview);
        pago.FailureCode.Should().Be("ORDER_ALREADY_SETTLED");
        pago.Consumed.Should().BeFalse();
        pago.PaidAmountUsd.Should().Be(80m);
        (await m.Db.PaymentHistories.CountAsync()).Should().Be(0);
        (await m.Db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Pending);
        m.Usuario.IsActive.Should().BeFalse();
        m.Log.Entradas.Should().Contain(e => e.Nivel == LogLevel.Critical);
    }

    [Fact]
    public async Task Si_el_indice_de_pedido_consumido_salta_es_un_doble_cobro()
    {
        // La red debajo de la transición atómica: UX_EntraxPayments_OrderId_Consumed.
        var m = await MontarAsync(activacion: _ =>
        {
            var act = new Mock<ISignupActivationService>();
            act.Setup(a => a.ActivatePendingSignupAsync(It.IsAny<Orders>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new DbUpdateException("guardado", new Exception(
                   "Cannot insert duplicate key row in object 'dbo.EntraxPayments' with unique index 'UX_EntraxPayments_OrderId_Consumed'.")));
            return act.Object;
        });

        var r = await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Sweep, default);

        r.Should().Be(EntraxSettlementOutcome.ToReview);
        var pago = await Fila(m);
        pago.FailureCode.Should().Be("ORDER_ALREADY_SETTLED");
        pago.Consumed.Should().BeFalse();
        (await m.Db.PaymentHistories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Otro_error_al_guardar_no_se_disfraza_de_doble_cobro()
    {
        var m = await MontarAsync(activacion: _ =>
        {
            var act = new Mock<ISignupActivationService>();
            act.Setup(a => a.ActivatePendingSignupAsync(It.IsAny<Orders>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new DbUpdateException("otra cosa", new Exception("timeout")));
            return act.Object;
        });

        var accion = () => m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Sweep, default);

        await accion.Should().ThrowAsync<DbUpdateException>();
    }

    // ── Fallido, caducado, tardío ────────────────────────────────────────────────

    [Theory]
    [InlineData("failed", EntraxPaymentStatus.Failed, EntraxSettlementOutcome.Failed)]
    [InlineData("expired", EntraxPaymentStatus.Expired, EntraxSettlementOutcome.Expired)]
    public async Task Fallido_o_caducado_devuelve_el_pedido_a_Pending_para_reintentar(
        string remoto, EntraxPaymentStatus esperado, EntraxSettlementOutcome resultado)
    {
        var m = await MontarAsync(remoto: EscenarioEntrax.Remoto(remoto));

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Poll, default)).Should().Be(resultado);

        (await Fila(m)).Status.Should().Be(esperado);
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
        (await m.Db.MemberProfiles.SingleAsync()).Status.Should().Be(MemberAccountStatus.Pending);
    }

    [Fact]
    public async Task Si_hay_otro_QR_vivo_del_mismo_pedido_no_vuelve_a_Pending()
    {
        var m = await MontarAsync(remoto: EscenarioEntrax.Remoto("expired"));
        await EscenarioEntrax.SembrarPagoAsync(m.Db, paymentId: Guid.NewGuid().ToString(), intento: 2);

        await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Sweep, default);

        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Processing);
    }

    [Fact]
    public async Task Pagado_tarde_tras_caducar_activa_si_el_pedido_sigue_sin_activar()
    {
        var m = await MontarAsync(pedido: OrderStatus.Pending, pago: EntraxPaymentStatus.Expired);

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Sweep, default))
            .Should().Be(EntraxSettlementOutcome.Activated);
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Completed);
    }

    [Fact]
    public async Task Pagado_tarde_cancela_la_cripto_que_esperaba_el_mismo_pedido()
    {
        var m = await MontarAsync(pago: EntraxPaymentStatus.Expired);
        m.Db.CryptoPaymentConfirmations.Add(new CryptoPaymentConfirmation
        {
            OrderId = EscenarioEntrax.OrderId, MemberId = EscenarioEntrax.MemberId, MemberEmail = EscenarioEntrax.Email,
            CryptoCurrency = "BTC", AmountDue = 80m, Purpose = CryptoPaymentPurpose.Signup,
            Status = CryptoPaymentConfirmationStatus.AwaitingPayment, CreatedBy = "seed", CreationDate = EscenarioEntrax.Ahora
        });
        await m.Db.SaveChangesAsync();

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default))
            .Should().Be(EntraxSettlementOutcome.Activated);

        var cripto = await m.Db.CryptoPaymentConfirmations.SingleAsync();
        cripto.Status.Should().Be(CryptoPaymentConfirmationStatus.Cancelled,
            "si no, alguien podría confirmar después una transferencia que no existe.");
        cripto.Notes.Should().Contain(EscenarioEntrax.PaymentId);
    }

    // ── Sin respuesta definitiva ─────────────────────────────────────────────────

    [Fact]
    public async Task Pendiente_en_Entrax_solo_anota_la_consulta()
    {
        var m = await MontarAsync(remoto: EscenarioEntrax.Remoto("pending"));

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Poll, default))
            .Should().Be(EntraxSettlementOutcome.StillPending);

        var pago = await Fila(m);
        pago.Status.Should().Be(EntraxPaymentStatus.AwaitingPayment);
        pago.LastCheckedAt.Should().Be(EscenarioEntrax.Ahora);
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Processing);
    }

    [Fact]
    public async Task Entrax_caido_no_cambia_nada()
    {
        var m = await MontarAsync();
        m.Cliente.Setup(c => c.GetPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(EntraxCall<EntraxPaymentState>.Fail(0, EntraxErrorCodes.Transport, "timeout", 30000));

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Webhook, default))
            .Should().Be(EntraxSettlementOutcome.ProviderUnavailable);

        (await Fila(m)).Status.Should().Be(EntraxPaymentStatus.AwaitingPayment);
        (await m.Db.EntraxApiCalls.SingleAsync()).ErrorCode.Should().Be(EntraxErrorCodes.Transport);
    }

    [Fact]
    public async Task Sin_paymentId_se_espera_un_rato_sin_llamar_a_Entrax()
    {
        var m = await MontarAsync(paymentId: null);

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Sweep, default))
            .Should().Be(EntraxSettlementOutcome.StillPending);
        m.Cliente.Verify(c => c.GetPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sin_paymentId_a_los_30_minutos_se_da_por_fallido_y_el_pedido_vuelve_a_Pending()
    {
        var m = await MontarAsync(paymentId: null, creado: EscenarioEntrax.Ahora.AddMinutes(-31));

        (await m.Liquidacion.SettleAsync(m.PagoId, EntraxSettlementSource.Sweep, default))
            .Should().Be(EntraxSettlementOutcome.Failed);
        (await Fila(m)).FailureCode.Should().Be("NO_PAYMENT_ID");
        (await m.Db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Un_pago_que_no_existe_es_NotFound()
    {
        var m = await MontarAsync();
        (await m.Liquidacion.SettleAsync("no-existe", EntraxSettlementSource.Admin, default))
            .Should().Be(EntraxSettlementOutcome.NotFound);
    }
}

/// <summary>Otro camino ya pasó el pedido a Completed: la transición atómica devuelve false.</summary>
internal sealed class CarreraPerdida : ISignupOrderClaim
{
    public Task<bool> TryCompleteAsync(Orders order, DateTime now, string actor, CancellationToken ct)
        => Task.FromResult(false);
}
