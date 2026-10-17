using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.CuotaAnual;

/// <summary>
/// Lo que enseña la tarjeta de la cuota (Plan K, K21, K22): estado, importe, vencimiento, último pago,
/// tokens de cuota, preferencia, últimos pagos y si se puede pagar ahora y por qué no, con las MISMAS
/// reglas que Billing (AnnualFeeEligibility). La preferencia solo la cambia un Ambassador.
/// </summary>
public class AnnualFeeOverviewServiceTests
{
    private const string Yo = "AMB-1";
    private static readonly DateTime Hoy = CuotaAnualSiembra.Ahora.Date;

    private static async Task<AppDbContext> BaseAsync(
        DateTime? vence = null, bool mantener = false, MemberType tipo = MemberType.Ambassador, bool conPlan = true,
        bool conEstado = true, DateTime? alta = null)
    {
        var db = ReportesDb.Nueva();
        if (conPlan) CuotaAnualSiembra.Plan(db);
        ReportesDb.Miembro(db, Yo, tipo: tipo, alta: alta ?? new DateTime(2026, 6, 5)).KeepQualified = mantener;
        if (conEstado) CuotaAnualSiembra.Estado(db, Yo, vence ?? new DateTime(2027, 6, 5), ultimoCobro: null);
        await db.SaveChangesAsync();
        return db;
    }

    private static AnnualFeeOverviewService Servicio(AppDbContext db) => new(db, CuotaAnualSiembra.Reloj());

    [Fact]
    public async Task A_un_cliente_no_le_aplica()
    {
        await using var db = await BaseAsync(tipo: MemberType.ExternalMember, conEstado: false);
        var r = await Servicio(db).GetAsync(Yo);
        r.Applies.Should().BeFalse();
        r.Status.Should().Be(AnnualFeeStatuses.NotApplicable);
        r.CanPayNow.Should().BeFalse();
    }

    [Fact]
    public async Task Al_dia_y_lejos_del_vencimiento_no_se_puede_pagar_todavia()
    {
        await using var db = await BaseAsync(vence: Hoy.AddDays(200));
        var r = await Servicio(db).GetAsync(Yo);
        r.Applies.Should().BeTrue();
        r.Status.Should().Be(AnnualFeeStatuses.UpToDate);
        r.Amount.Should().Be(99m);
        r.NextDueDate.Should().Be(Hoy.AddDays(200));
        r.CanPayNow.Should().BeFalse();
        r.NotPayableReason.Should().Be(AnnualFeeErrors.NotDue);
    }

    [Fact]
    public async Task Dentro_de_los_30_dias_vence_pronto_y_se_puede_pagar()
    {
        await using var db = await BaseAsync(vence: Hoy.AddDays(10));
        var r = await Servicio(db).GetAsync(Yo);
        r.Status.Should().Be(AnnualFeeStatuses.DueSoon);
        r.CanPayNow.Should().BeTrue();
        r.NotPayableReason.Should().BeNull();
    }

    [Fact]
    public async Task Vencida_se_puede_pagar()
    {
        await using var db = await BaseAsync(vence: Hoy.AddDays(-5));
        var r = await Servicio(db).GetAsync(Yo);
        r.Status.Should().Be(AnnualFeeStatuses.Due);
        r.CanPayNow.Should().BeTrue();
    }

    [Fact]
    public async Task Con_KeepQualified_esta_exenta()
    {
        await using var db = await BaseAsync(vence: Hoy.AddDays(-5), mantener: true);
        var r = await Servicio(db).GetAsync(Yo);
        r.Status.Should().Be(AnnualFeeStatuses.Waived);
        r.NotPayableReason.Should().Be(AnnualFeeErrors.Waived);
    }

    [Fact]
    public async Task Con_un_pago_a_conciliar_esta_en_revision_y_no_se_puede_pagar()
    {
        await using var db = await BaseAsync(vence: Hoy.AddDays(-5));
        CuotaAnualSiembra.Pago(db, Yo, $"estado-{Yo}-AnnualFee", Hoy.AddDays(-5), AnnualFeePaymentStatus.NeedsReconciliation, Hoy.AddDays(-4));
        await db.SaveChangesAsync();

        var r = await Servicio(db).GetAsync(Yo);

        r.Status.Should().Be(AnnualFeeStatuses.UnderReview);
        r.CanPayNow.Should().BeFalse();
        r.NotPayableReason.Should().Be(AnnualFeeErrors.NeedsReview);
    }

    [Fact]
    public async Task Sin_estado_todavia_enseña_el_vencimiento_que_tendra()
    {
        await using var db = await BaseAsync(conEstado: false, alta: new DateTime(2025, 6, 1));
        var r = await Servicio(db).GetAsync(Yo);
        r.NextDueDate.Should().Be(new DateTime(2028, 6, 1)); // el aniversario de 2027 ya pasó: no se cobra hacia atrás
        r.Status.Should().Be(AnnualFeeStatuses.UpToDate);
    }

    [Fact]
    public async Task Sin_plan_configurado_no_se_puede_pagar()
    {
        await using var db = await BaseAsync(conPlan: false, vence: Hoy.AddDays(-5));
        var r = await Servicio(db).GetAsync(Yo);
        r.CanPayNow.Should().BeFalse();
        r.NotPayableReason.Should().Be(AnnualFeeErrors.PlanNotConfigured);
        r.Amount.Should().BeNull();
    }

    [Fact]
    public async Task Cuenta_los_tokens_de_cuota_vivos_y_solo_esos()
    {
        await using var db = await BaseAsync();
        CuotaAnualSiembra.Token(db, Yo, AnnualFeeRules.AnnualBizCenterTokenTypeId);
        CuotaAnualSiembra.Token(db, Yo, AnnualFeeRules.AnnualFeeTokenTypeId);
        CuotaAnualSiembra.Token(db, Yo, 24);
        CuotaAnualSiembra.Token(db, Yo, estado: TokenInstanceStatus.Used);
        CuotaAnualSiembra.Token(db, "AMB-2");
        await db.SaveChangesAsync();

        (await Servicio(db).GetAsync(Yo)).AvailableFeeTokens.Should().Be(2);
    }

    [Fact]
    public async Task Enseña_los_ultimos_cinco_pagos_del_mas_reciente_al_mas_antiguo()
    {
        await using var db = await BaseAsync();
        for (var i = 0; i < 7; i++)
            CuotaAnualSiembra.Pago(db, Yo, $"estado-{Yo}-AnnualFee", Hoy.AddYears(-i), AnnualFeePaymentStatus.Failed, Hoy.AddDays(-i));
        await db.SaveChangesAsync();

        var pagos = (await Servicio(db).GetAsync(Yo)).RecentPayments;

        pagos.Should().HaveCount(5);
        pagos.Select(p => p.Date).Should().BeInDescendingOrder();
        pagos[0].Method.Should().Be(AnnualFeeMethods.CommissionBalance);
        pagos[0].Status.Should().Be(AnnualFeePaymentStatuses.Failed);
    }

    [Fact]
    public async Task La_preferencia_se_guarda_y_se_lee()
    {
        await using var db = await BaseAsync();
        var s = Servicio(db);

        (await s.SetFeesFromBalanceAsync(Yo, false, "user-1")).Should().BeTrue();
        (await s.GetAsync(Yo)).FeesFromBalance.Should().BeFalse();
        (await db.MemberProfiles.AsNoTracking().SingleAsync()).FeesFromBalance.Should().BeFalse();

        (await s.SetFeesFromBalanceAsync(Yo, true, "user-1")).Should().BeTrue();
        (await s.GetAsync(Yo)).FeesFromBalance.Should().BeTrue();
    }

    [Fact]
    public async Task Un_cliente_no_tiene_preferencia_de_cuotas()
    {
        await using var db = await BaseAsync(tipo: MemberType.ExternalMember, conEstado: false);
        (await Servicio(db).SetFeesFromBalanceAsync(Yo, false, "user-1")).Should().BeFalse();
    }

    [Fact]
    public async Task Con_la_membresia_en_HoldByBilling_no_se_puede_pagar_ahora()
    {
        await using var db = await BaseAsync(vence: Hoy.AddDays(-5));
        ReportesDb.Suscripcion(db, Yo, 3, MembershipStatus.HoldByBilling, Hoy.AddDays(-100));
        await db.SaveChangesAsync();

        var r = await Servicio(db).GetAsync(Yo);

        r.CanPayNow.Should().BeFalse();
        r.NotPayableReason.Should().Be(AnnualFeeErrors.MembershipOnHold);
    }

    [Fact]
    public async Task Con_la_membresia_cancelada_y_la_cuenta_inactiva_se_puede_pagar_ahora()
    {
        // Decisión de Nick (M-8): quien canceló puede mantener su Biz Center pagando la cuota.
        await using var db = await BaseAsync(vence: Hoy.AddDays(-5));
        (await db.MemberProfiles.SingleAsync()).Status = MemberAccountStatus.Inactive;
        ReportesDb.Suscripcion(db, Yo, 3, MembershipStatus.Cancelled, Hoy.AddDays(-100));
        await db.SaveChangesAsync();

        (await Servicio(db).GetAsync(Yo)).CanPayNow.Should().BeTrue();
    }
}
