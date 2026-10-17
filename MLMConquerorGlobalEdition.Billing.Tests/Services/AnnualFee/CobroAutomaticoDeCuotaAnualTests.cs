using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// El cobro automático de la cuota (Plan K): apagado por defecto (K13); exclusiones del legado (K5);
/// token siempre, comisiones solo con la preferencia (K6, K7); sin fondos, la cadencia del plan y al
/// final Stopped, sin tocar la membresía (K12); todo bajo el candado del miembro.
/// </summary>
public class CobroAutomaticoDeCuotaAnualTests
{
    private const string Miembro = "AMB-1";
    private static readonly DateTime Ahora = CuotaAnualDb.Ahora;
    private static readonly DateTime Vence = new(2027, 6, 5);

    internal static AnnualFeeChargeService Servicio(
        AppDbContext db, bool encendido = true, IMemberFundsGate? candado = null, Mock<IEWalletFunds>? ewallet = null,
        IAnnualFeeTokenConsumer? tokens = null, ICommissionBalanceService? comisiones = null)
    {
        var reloj  = CuotaAnualDb.Reloj();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [AnnualFeeRules.AutoChargeEnabledConfigKey] = encendido ? "true" : "false"
        }).Build();

        return new AnnualFeeChargeService(
            db, reloj, candado ?? new TransactionOnlyGate(db), comisiones ?? new CommissionBalanceService(db, reloj),
            (ewallet ?? new Mock<IEWalletFunds>()).Object, tokens ?? new AnnualFeeTokenConsumer(db),
            new AnnualFeeStateEnrollment(db, reloj, NullLogger<AnnualFeeStateEnrollment>.Instance),
            new RecurringBillingScheduler(), config, NullLogger<AnnualFeeChargeService>.Instance);
    }

    private static async Task<AppDbContext> ConMiembroAsync(
        bool conSaldo = true, MemberAccountStatus estado = MemberAccountStatus.Active, bool mantener = false,
        DateTime? vence = null, RecurringBillingStatus estadoCobro = RecurringBillingStatus.Active,
        DateTime? ultimoCobro = null, DateTime? alta = null)
    {
        var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, Miembro, alta: alta, estado: estado, mantener: mantener, conSaldo: conSaldo);
        await CuotaAnualDb.EstadoAsync(db, Miembro, vence ?? Vence, estadoCobro, ultimoCobro);
        return db;
    }

    private static Task<SubscriptionBillingState> EstadoAsync(AppDbContext db) =>
        db.SubscriptionBillingStates.AsNoTracking().SingleAsync();

    [Fact]
    public async Task Con_el_interruptor_apagado_no_hace_nada()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        var r = await Servicio(db, encendido: false).ChargeDueAsync("anual-" + Miembro, force: false);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Skipped);
        r.Value.FailureReason.Should().Be(AnnualFeeErrors.AutoChargeDisabled);
        (await db.Orders.CountAsync()).Should().Be(0);
        (await EstadoAsync(db)).NextAttemptDate.Should().Be(Vence);
    }

    [Fact]
    public async Task Con_un_token_de_cuota_paga_aunque_la_preferencia_este_apagada()
    {
        await using var db = await ConMiembroAsync(conSaldo: false);
        await CuotaAnualDb.TokenAsync(db, Miembro, codigo: "CUOTA-1");
        var candado = new TransactionOnlyGate(db);

        var r = await Servicio(db, candado: candado).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Success);
        r.Value.FundingSource.Should().Be(AnnualFeeMethods.Token);
        candado.Entered.Should().Contain(Miembro);

        var pago = await db.AnnualFeePayments.SingleAsync();
        pago.Status.Should().Be(AnnualFeePaymentStatus.Completed);
        pago.Method.Should().Be(AnnualFeePaymentMethod.Token);
        pago.Source.Should().Be(AnnualFeePaymentSource.Automatic);
        pago.Amount.Should().Be(0m);
        pago.TokenCode.Should().Be("CUOTA-1");
        pago.CycleDueDate.Should().Be(Vence);
        pago.IdempotencyKey.Should().BeNull();

        var pedido = await db.Orders.SingleAsync();
        pedido.Status.Should().Be(OrderStatus.Paid);
        pedido.TotalAmount.Should().Be(0m);
        pedido.MembershipSubscriptionId.Should().BeNull();
        (await db.OrderDetails.CountAsync()).Should().Be(0);
        (await db.PaymentHistories.CountAsync()).Should().Be(0);

        (await db.TokenTransactions.SingleAsync(t => t.ReferenceId == "CUOTA-1")).UsedOnOrderId.Should().Be(pedido.Id);

        var estado = await EstadoAsync(db);
        // K4 revisado: el ciclo vencía el 2027-06-05 y se paga el 2027-06-10 (tarde): un año desde el pago.
        estado.NextBillingDate.Should().Be(new DateTime(2028, 6, 10));
        estado.NextAttemptDate.Should().Be(new DateTime(2028, 6, 10));
        estado.LastSuccessfulBillingDate.Should().Be(Ahora);
        estado.Status.Should().Be(RecurringBillingStatus.Active);
        estado.BillingAnchorDate.Should().Be(Ahora.Date); // pagado tarde: el ancla pasa a la fecha de pago (M3)

        var intento = await db.RecurringBillingAttempts.SingleAsync();
        intento.FundingSource.Should().Be(RecurringFundingSource.Token);
        intento.Outcome.Should().Be(RecurringAttemptOutcome.Success);
        intento.ProductId.Should().Be(CuotaAnualDb.Producto);
    }

    [Fact]
    public async Task Sin_token_y_con_la_preferencia_paga_con_comisiones()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        var r = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Success);
        r.Value.FundingSource.Should().Be(AnnualFeeMethods.CommissionBalance);

        var pago = await db.AnnualFeePayments.SingleAsync();
        pago.Method.Should().Be(AnnualFeePaymentMethod.CommissionBalance);
        pago.Amount.Should().Be(99m);
        pago.PaymentHistoryId.Should().Be(r.Value.PaymentHistoryId);

        (await db.Orders.SingleAsync()).TotalAmount.Should().Be(99m);
        (await db.CommissionEarnings.SingleAsync(e => e.Amount < 0)).Amount.Should().Be(-99m);
        (await db.RecurringBillingAttempts.SingleAsync()).CommissionDeductionEarningId.Should().NotBeNull();
        (await EstadoAsync(db)).NextBillingDate.Should().Be(new DateTime(2028, 6, 10)); // K4 revisado: tarde
    }

    [Fact]
    public async Task Con_la_preferencia_apagada_no_toca_las_comisiones_y_aplica_la_cadencia()
    {
        await using var db = await ConMiembroAsync(conSaldo: false);
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        var r = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Failed);
        r.Value.FailureReason.Should().Be(AnnualFeeErrors.NoFunds);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(0);
        (await db.AnnualFeePayments.CountAsync()).Should().Be(0);

        var estado = await EstadoAsync(db);
        estado.Status.Should().Be(RecurringBillingStatus.Retrying);
        estado.CurrentAttemptIndex.Should().Be(1);
        estado.NextAttemptDate.Should().Be(Ahora.Date.AddDays(1));
        estado.NextBillingDate.Should().Be(Vence);

        var intento = await db.RecurringBillingAttempts.SingleAsync();
        intento.Outcome.Should().Be(RecurringAttemptOutcome.Failed);
        intento.FundingSource.Should().Be(RecurringFundingSource.Token);
    }

    [Fact]
    public async Task Agotada_la_cadencia_para_el_estado_sin_tocar_la_membresia_ni_la_cuenta()
    {
        await using var db = await ConMiembroAsync(estadoCobro: RecurringBillingStatus.Retrying);
        var estado = await db.SubscriptionBillingStates.SingleAsync();
        estado.CurrentAttemptIndex = 7; // la cadencia del plan anual tiene 7 reintentos
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-ta", MemberId = Miembro, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Active,
            StartDate = Ahora.AddDays(-10), CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();

        var r = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Failed);
        (await EstadoAsync(db)).Status.Should().Be(RecurringBillingStatus.Stopped);
        (await db.MembershipSubscriptions.AsNoTracking().SingleAsync()).SubscriptionStatus.Should().Be(MembershipStatus.Active);
        (await db.MemberProfiles.AsNoTracking().SingleAsync()).Status.Should().Be(MemberAccountStatus.Active);
    }

    [Fact]
    public async Task Una_cuenta_terminada_para_el_estado()
    {
        await using var db = await ConMiembroAsync(estado: MemberAccountStatus.Terminated);

        var r = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Skipped);
        r.Value.FailureReason.Should().Be("ACCOUNT_CLOSED");
        (await EstadoAsync(db)).Status.Should().Be(RecurringBillingStatus.Stopped);
        (await db.RecurringBillingAttempts.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(MemberAccountStatus.Suspended, false, "ACCOUNT_SUSPENDED")]
    [InlineData(MemberAccountStatus.Pending, false, "ACCOUNT_PENDING")]
    [InlineData(MemberAccountStatus.Active, true, "KEEP_QUALIFIED")]
    public async Task Suspendida_pendiente_o_KeepQualified_se_mira_manana_sin_cobrar_ni_avanzar(
        MemberAccountStatus estadoCuenta, bool mantener, string razon)
    {
        await using var db = await ConMiembroAsync(estado: estadoCuenta, mantener: mantener);
        await CuotaAnualDb.TokenAsync(db, Miembro);

        var r = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.FailureReason.Should().Be(razon);
        var estado = await EstadoAsync(db);
        estado.NextAttemptDate.Should().Be(Ahora.Date.AddDays(1));
        estado.NextBillingDate.Should().Be(Vence);
        (await db.AnnualFeePayments.CountAsync()).Should().Be(0);
        (await AnnualFeeTokenQueriesCount(db)).Should().Be(1);
    }

    private static Task<int> AnnualFeeTokenQueriesCount(AppDbContext db) =>
        MLMConquerorGlobalEdition.Repository.Services.AnnualFee.AnnualFeeTokenQueries.Available(db, Miembro, Ahora).CountAsync();

    [Fact]
    public async Task Cobrada_hace_poco_se_reprograma_a_un_anio_del_cobro()
    {
        await using var db = await ConMiembroAsync(ultimoCobro: new DateTime(2027, 2, 1));
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        var r = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.FailureReason.Should().Be("RECENTLY_BILLED");
        (await EstadoAsync(db)).NextBillingDate.Should().Be(new DateTime(2028, 2, 1));
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Si_aun_no_vence_se_reprograma_a_su_vencimiento()
    {
        await using var db = await ConMiembroAsync(vence: new DateTime(2027, 6, 20));
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        var r = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.FailureReason.Should().Be("NOT_DUE");
        (await EstadoAsync(db)).NextAttemptDate.Should().Be(new DateTime(2027, 6, 20));
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_estado_parado_no_se_cobra_y_forzado_se_revive_y_se_cobra()
    {
        await using var db = await ConMiembroAsync(estadoCobro: RecurringBillingStatus.Stopped);
        await CuotaAnualDb.TokenAsync(db, Miembro);

        (await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false)).Value!.Outcome
            .Should().Be(AnnualFeeOutcomes.Skipped);

        // Revisión I-3: el forzado también exige el interruptor encendido.
        var forzado = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: true);

        forzado.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Success);
        (await EstadoAsync(db)).Status.Should().Be(RecurringBillingStatus.Active);
    }

    [Fact]
    public async Task Un_ciclo_con_un_pago_vivo_no_se_cobra_otra_vez()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);
        db.AnnualFeePayments.Add(new AnnualFeePayment
        {
            MemberId = Miembro, SubscriptionBillingStateId = "anual-" + Miembro, CycleDueDate = Vence, Amount = 99m,
            Method = AnnualFeePaymentMethod.EWallet, Source = AnnualFeePaymentSource.Automatic,
            Status = AnnualFeePaymentStatus.NeedsReconciliation, OrderId = "ord-antes", CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();

        var r = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Skipped);
        r.Value.FailureReason.Should().Be(AnnualFeeErrors.NeedsReview);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(0);
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Con_el_candado_ocupado_no_mueve_nada()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        var r = await Servicio(db, candado: new BusyGate()).ChargeDueAsync("anual-" + Miembro, force: false);

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(AnnualFeeErrors.Busy);
        (await db.Orders.CountAsync()).Should().Be(0);
        (await EstadoAsync(db)).NextAttemptDate.Should().Be(Vence);
    }

    [Fact]
    public async Task Un_estado_de_membresia_no_lo_cobra_este_servicio()
    {
        await using var db = await ConMiembroAsync();
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "membresia", Purpose = BillingStatePurpose.Membership, MembershipSubscriptionId = "sub-1",
            MemberId = Miembro, RecurringBillingPlanId = CuotaAnualDb.PlanAnual, NextBillingDate = Vence,
            NextAttemptDate = Vence, CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();

        var r = await Servicio(db).ChargeDueAsync("membresia", force: false);

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(AnnualFeeErrors.StateNotFound);
    }

    [Fact]
    public async Task Pagada_el_dia_del_vencimiento_conserva_el_ancla()
    {
        await using var db = await ConMiembroAsync(vence: Ahora.Date);
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        (await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false)).Value!.Outcome.Should().Be(AnnualFeeOutcomes.Success);

        var estado = await EstadoAsync(db);
        estado.BillingAnchorDate.Should().Be(new DateTime(2026, 6, 5));
        // Vencimiento fuera del ancla (2027-06-10 no es un aniversario del 2026-06-05): un año desde el vencimiento.
        estado.NextBillingDate.Should().Be(new DateTime(2028, 6, 10));
    }

    [Fact]
    public async Task Con_la_membresia_en_HoldByBilling_no_se_cobra_y_se_mira_manana()
    {
        // Decisión de Nick (2026-10-17): la membresía en HoldByBilling queda excluida de la cuota.
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.TokenAsync(db, Miembro);
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-ta", MemberId = Miembro, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.HoldByBilling,
            StartDate = Ahora.AddDays(-100), CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();

        var r = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Skipped);
        r.Value.FailureReason.Should().Be(MLMConquerorGlobalEdition.Repository.Services.AnnualFee.AnnualFeeEligibility.ReasonMembershipOnHold);
        (await db.AnnualFeePayments.CountAsync()).Should().Be(0);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(0);
        var estado = await EstadoAsync(db);
        estado.NextAttemptDate.Should().Be(Ahora.Date.AddDays(1));
        estado.NextBillingDate.Should().Be(Vence);
    }

    [Fact]
    public async Task Las_comisiones_aun_no_pagables_no_pagan_la_cuota()
    {
        await using var db = await ConMiembroAsync();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m, pagable: Ahora.AddDays(3));

        var r = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Failed);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(0);
    }

    [Fact]
    public async Task Con_la_membresia_cancelada_no_se_cobra_y_se_mira_manana()
    {
        // Decisión de Nick (M-8): quien canceló no paga la cuota en automático hasta que reactive.
        await using var db = await ConMiembroAsync(estado: MemberAccountStatus.Inactive);
        await CuotaAnualDb.TokenAsync(db, Miembro);
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-ta", MemberId = Miembro, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Cancelled,
            StartDate = Ahora.AddDays(-100), CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();

        var r = await Servicio(db).ChargeDueAsync("anual-" + Miembro, force: false);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Skipped);
        r.Value.FailureReason.Should().Be(MLMConquerorGlobalEdition.Repository.Services.AnnualFee.AnnualFeeEligibility.ReasonMembershipCancelled);
        (await db.AnnualFeePayments.CountAsync()).Should().Be(0);
        var estado = await EstadoAsync(db);
        estado.NextAttemptDate.Should().Be(Ahora.Date.AddDays(1));
        estado.NextBillingDate.Should().Be(Vence);
    }
}
