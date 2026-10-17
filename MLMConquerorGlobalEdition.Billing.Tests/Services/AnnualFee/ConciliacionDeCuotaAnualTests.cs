using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// Lo dudoso lo cierra tesorería (Plan K, K11): cobrado → se completa y avanza el ciclo desde el día del
/// débito; no cobrado → fallido y el ciclo queda libre. Un pago que se quedó Pending pasa a conciliar.
/// </summary>
public class ConciliacionDeCuotaAnualTests
{
    private const string Miembro = "AMB-1";
    private static readonly DateTime Vence = new(2027, 6, 5);

    private static async Task<(AppDbContext Db, AnnualFeePayment Pago)> ConPagoAsync(
        AnnualFeePaymentStatus estado = AnnualFeePaymentStatus.NeedsReconciliation, DateTime? creado = null)
    {
        var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, Miembro);
        await CuotaAnualDb.EstadoAsync(db, Miembro, Vence);
        var pedido = new Orders
        {
            MemberId = Miembro, OrderNo = "AF0606-1", TotalAmount = 99m, Status = OrderStatus.Processing,
            OrderDate = CuotaAnualDb.Ahora.AddDays(-4), CreatedBy = "prueba"
        };
        db.Orders.Add(pedido);
        var pago = new AnnualFeePayment
        {
            MemberId = Miembro, SubscriptionBillingStateId = "anual-" + Miembro, CycleDueDate = Vence, Amount = 99m,
            Method = AnnualFeePaymentMethod.EWallet, Source = AnnualFeePaymentSource.Automatic, Status = estado,
            OrderId = pedido.Id, CreatedBy = "annual-fee-engine", CreationDate = creado ?? new DateTime(2027, 6, 6, 5, 0, 0)
        };
        db.AnnualFeePayments.Add(pago);
        await db.SaveChangesAsync();
        // AuditInterceptor pone CreationDate = ahora al insertar: la fecha del débito se fija después.
        pago.CreationDate = creado ?? new DateTime(2027, 6, 6, 5, 0, 0);
        await db.SaveChangesAsync();
        return (db, pago);
    }

    [Fact]
    public async Task Cobrado_se_completa_y_avanza_el_ciclo_desde_el_dia_del_debito()
    {
        var (db, pago) = await ConPagoAsync();
        await using var _ = db;

        var r = await CobroAutomaticoDeCuotaAnualTests.Servicio(db).ReconcileAsync(pago.Id,
            new ReconcileAnnualFeePaymentRequest { Charged = true, GatewayReference = "IPY-123", Notes = "visto en i-Payout" },
            "tesoreria@empresa.com");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Status.Should().Be(AnnualFeePaymentStatuses.Completed);
        var guardado = await db.AnnualFeePayments.AsNoTracking().SingleAsync();
        guardado.Status.Should().Be(AnnualFeePaymentStatus.Completed);
        guardado.Notes.Should().Contain("visto en i-Payout");
        var ph = await db.PaymentHistories.SingleAsync();
        ph.GatewayName.Should().Be("eWallet");
        ph.GatewayTransactionId.Should().Be("IPY-123");
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Paid);

        var estado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        estado.NextBillingDate.Should().Be(new DateTime(2028, 6, 6)); // K4 revisado: debitado un día tarde
        estado.LastSuccessfulBillingDate.Should().Be(new DateTime(2027, 6, 6, 5, 0, 0));
    }

    [Fact]
    public async Task No_cobrado_queda_fallido_y_libera_el_ciclo()
    {
        var (db, pago) = await ConPagoAsync();
        await using var _ = db;

        var r = await CobroAutomaticoDeCuotaAnualTests.Servicio(db).ReconcileAsync(pago.Id,
            new ReconcileAnnualFeePaymentRequest { Charged = false }, "tesoreria@empresa.com");

        r.IsSuccess.Should().BeTrue();
        (await db.AnnualFeePayments.AsNoTracking().SingleAsync()).Status.Should().Be(AnnualFeePaymentStatus.Failed);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);
        (await db.SubscriptionBillingStates.AsNoTracking().SingleAsync()).NextBillingDate.Should().Be(Vence);
        (await MLMConquerorGlobalEdition.Repository.Services.AnnualFee.AnnualFeePaymentQueries
            .LiveForCycle(db, "anual-" + Miembro, Vence).CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Solo_se_concilia_lo_que_esta_a_conciliar()
    {
        var (db, pago) = await ConPagoAsync(AnnualFeePaymentStatus.Completed);
        await using var _ = db;

        var r = await CobroAutomaticoDeCuotaAnualTests.Servicio(db).ReconcileAsync(pago.Id,
            new ReconcileAnnualFeePaymentRequest { Charged = true }, "tesoreria");

        r.ErrorCode.Should().Be(AnnualFeeErrors.NotUnderReview);
    }

    [Fact]
    public async Task Un_pago_que_no_existe_es_404()
    {
        await using var db = CuotaAnualDb.Nueva();

        var r = await CobroAutomaticoDeCuotaAnualTests.Servicio(db).ReconcileAsync("no-existe",
            new ReconcileAnnualFeePaymentRequest { Charged = true }, "tesoreria");

        r.ErrorCode.Should().Be(AnnualFeeErrors.PaymentNotFound);
    }

    [Fact]
    public async Task Un_Pending_de_mas_de_30_minutos_pasa_a_conciliar_y_uno_reciente_no()
    {
        var (db, viejo) = await ConPagoAsync(AnnualFeePaymentStatus.Pending, CuotaAnualDb.Ahora.AddMinutes(-40));
        await using var _ = db;
        var reciente = new AnnualFeePayment
        {
            MemberId = "AMB-2", SubscriptionBillingStateId = "anual-AMB-2", CycleDueDate = Vence, Amount = 99m,
            Method = AnnualFeePaymentMethod.EWallet, Source = AnnualFeePaymentSource.PayNow,
            Status = AnnualFeePaymentStatus.Pending, OrderId = "ord-2", CreatedBy = "user-2"
        };
        db.AnnualFeePayments.Add(reciente);
        await db.SaveChangesAsync();
        reciente.CreationDate = CuotaAnualDb.Ahora.AddMinutes(-10);
        await db.SaveChangesAsync();

        var movidos = await CobroAutomaticoDeCuotaAnualTests.Servicio(db).MoveStalePendingToReconciliationAsync(30, "barrido");

        movidos.Should().Be(1);
        (await db.AnnualFeePayments.AsNoTracking().SingleAsync(p => p.Id == viejo.Id)).Status
            .Should().Be(AnnualFeePaymentStatus.NeedsReconciliation);
        (await db.AnnualFeePayments.AsNoTracking().SingleAsync(p => p.MemberId == "AMB-2")).Status
            .Should().Be(AnnualFeePaymentStatus.Pending);
    }

    // ── Revisión del Bloque 3 (M-3, M-4, M-7) ─────────────────────────────────

    [Fact]
    public async Task Queda_auditado_quien_cuando_referencia_y_resultado()
    {
        var (db, pago) = await ConPagoAsync();
        await using var _ = db;

        await CobroAutomaticoDeCuotaAnualTests.Servicio(db).ReconcileAsync(pago.Id,
            new ReconcileAnnualFeePaymentRequest { Charged = true, GatewayReference = "IPY-777", Notes = "visto" },
            "tesoreria@empresa.com");

        var guardado = await db.AnnualFeePayments.AsNoTracking().SingleAsync();
        guardado.Notes.Should().Contain("tesoreria@empresa.com").And.Contain("IPY-777").And.Contain("charged")
            .And.Contain(CuotaAnualDb.Ahora.ToString("yyyy-MM-dd HH:mm")).And.Contain("visto");
        guardado.LastUpdateBy.Should().Be("tesoreria@empresa.com");
    }

    [Fact]
    public async Task No_cobrado_tambien_queda_auditado()
    {
        var (db, pago) = await ConPagoAsync();
        await using var _ = db;

        await CobroAutomaticoDeCuotaAnualTests.Servicio(db).ReconcileAsync(pago.Id,
            new ReconcileAnnualFeePaymentRequest { Charged = false }, "tesoreria@empresa.com");

        (await db.AnnualFeePayments.AsNoTracking().SingleAsync()).Notes
            .Should().Contain("not charged").And.Contain("tesoreria@empresa.com");
    }

    [Fact]
    public async Task Una_referencia_demasiado_larga_se_rechaza_sin_tocar_nada()
    {
        var (db, pago) = await ConPagoAsync();
        await using var _ = db;

        var r = await CobroAutomaticoDeCuotaAnualTests.Servicio(db).ReconcileAsync(pago.Id,
            new ReconcileAnnualFeePaymentRequest { Charged = true, GatewayReference = new string('R', AnnualFeeRules.GatewayReferenceMaxLength + 1) },
            "tesoreria");

        r.ErrorCode.Should().Be(AnnualFeeErrors.GatewayReferenceTooLong);
        (await db.AnnualFeePayments.AsNoTracking().SingleAsync()).Status.Should().Be(AnnualFeePaymentStatus.NeedsReconciliation);
        (await db.PaymentHistories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Unas_notas_demasiado_largas_se_rechazan()
    {
        var (db, pago) = await ConPagoAsync();
        await using var _ = db;

        var r = await CobroAutomaticoDeCuotaAnualTests.Servicio(db).ReconcileAsync(pago.Id,
            new ReconcileAnnualFeePaymentRequest { Charged = false, Notes = new string('n', AnnualFeeRules.ReconcileNotesMaxLength + 1) },
            "tesoreria");

        r.ErrorCode.Should().Be(AnnualFeeErrors.ReconcileNotesTooLong);
    }

    [Fact]
    public async Task Cobrado_con_el_estado_ya_movido_se_completa_y_avisa_con_su_codigo()
    {
        // El ciclo lo pagó otra vía mientras este débito estaba en revisión: posible doble cobro.
        var (db, pago) = await ConPagoAsync();
        await using var _ = db;
        var estado = await db.SubscriptionBillingStates.SingleAsync();
        estado.NextBillingDate = new DateTime(2028, 6, 5);
        await db.SaveChangesAsync();

        var r = await CobroAutomaticoDeCuotaAnualTests.Servicio(db).ReconcileAsync(pago.Id,
            new ReconcileAnnualFeePaymentRequest { Charged = true, GatewayReference = "IPY-9" }, "tesoreria");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Warning.Should().Be(AnnualFeeErrors.ReconciledCycleAlreadyAdvanced);
        var guardado = await db.AnnualFeePayments.AsNoTracking().SingleAsync();
        guardado.Status.Should().Be(AnnualFeePaymentStatus.Completed);
        guardado.Notes.Should().Contain(AnnualFeeErrors.ReconciledCycleAlreadyAdvanced);
        (await db.SubscriptionBillingStates.AsNoTracking().SingleAsync()).NextBillingDate.Should().Be(new DateTime(2028, 6, 5));
    }
}
