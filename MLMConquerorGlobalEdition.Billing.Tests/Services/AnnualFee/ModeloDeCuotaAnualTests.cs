using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// El esquema de la cuota anual (Plan K, K1, K2, K10, K23), mirado en el modelo de diseño de SQL Server
/// sin conectarse. Lo que de verdad hace cumplir estas reglas (índices filtrados y CHECK) solo lo aplica
/// SQL Server: SqlServerCuotaAnualTests lo comprueba contra la base.
/// </summary>
public class ModeloDeCuotaAnualTests
{
    private static readonly IModel Modelo = CrearModelo();

    private static IModel CrearModelo()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=.;Database=modelo-sin-conexion;Integrated Security=true;TrustServerCertificate=True")
            .Options);
        return db.GetService<IDesignTimeModel>().Model;
    }

    private static IEntityType Entidad<T>() => Modelo.FindEntityType(typeof(T))!;

    private static IIndex Indice<T>(params string[] propiedades) =>
        Entidad<T>().GetIndexes().Single(i => i.Properties.Select(p => p.Name).SequenceEqual(propiedades)
                                              && (i.GetDatabaseName() ?? "").StartsWith("IX_"));

    [Fact]
    public void El_estado_de_cobro_admite_estados_sin_suscripcion()
    {
        var p = Entidad<SubscriptionBillingState>().FindProperty(nameof(SubscriptionBillingState.MembershipSubscriptionId))!;
        p.IsNullable.Should().BeTrue();
        p.GetMaxLength().Should().Be(100);
    }

    [Fact]
    public void La_suscripcion_sigue_siendo_unica_entre_los_estados_que_la_tienen()
    {
        var i = Indice<SubscriptionBillingState>(nameof(SubscriptionBillingState.MembershipSubscriptionId));
        i.IsUnique.Should().BeTrue();
        i.GetFilter().Should().Be("[MembershipSubscriptionId] IS NOT NULL");
    }

    [Fact]
    public void Un_miembro_tiene_como_mucho_un_estado_de_cuota_anual()
    {
        var i = Entidad<SubscriptionBillingState>().GetIndexes()
            .Single(x => x.GetDatabaseName() == "UX_SubscriptionBillingStates_AnnualFeeMember");
        i.IsUnique.Should().BeTrue();
        i.Properties.Select(p => p.Name).Should().Equal(nameof(SubscriptionBillingState.MemberId));
        i.GetFilter().Should().Be("[Purpose] = 2");
    }

    [Fact]
    public void El_CHECK_ata_el_proposito_a_tener_o_no_suscripcion()
    {
        var ck = Entidad<SubscriptionBillingState>().GetCheckConstraints()
            .Single(c => c.ModelName == "CK_SubscriptionBillingStates_Purpose");
        ck.Sql.Should().Contain("[Purpose] = 1 AND [MembershipSubscriptionId] IS NOT NULL")
              .And.Contain("[Purpose] = 2 AND [MembershipSubscriptionId] IS NULL");
    }

    [Fact]
    public void El_proposito_por_defecto_es_la_membresia()
        => new SubscriptionBillingState().Purpose.Should().Be(BillingStatePurpose.Membership);

    [Fact]
    public void Solo_un_plan_puede_ser_el_de_la_cuota_anual()
    {
        var i = Entidad<RecurringBillingPlan>().GetIndexes()
            .Single(x => x.GetDatabaseName() == "UX_RecurringBillingPlans_AnnualFee");
        i.IsUnique.Should().BeTrue();
        i.GetFilter().Should().Be("[IsAnnualFeePlan] = 1");
    }

    [Fact]
    public void La_preferencia_de_pagar_con_saldo_empieza_encendida()
    {
        new MemberProfile().FeesFromBalance.Should().BeTrue();
        Entidad<MemberProfile>().FindProperty(nameof(MemberProfile.FeesFromBalance))!.GetDefaultValue().Should().Be(true);
    }

    [Fact]
    public void Un_ciclo_no_puede_tener_dos_pagos_vivos()
    {
        var i = Entidad<AnnualFeePayment>().GetIndexes()
            .Single(x => x.GetDatabaseName() == "UX_AnnualFeePayments_LivePerCycle");
        i.IsUnique.Should().BeTrue();
        i.Properties.Select(p => p.Name).Should().Equal(
            nameof(AnnualFeePayment.SubscriptionBillingStateId), nameof(AnnualFeePayment.CycleDueDate));
        i.GetFilter().Should().Be("[Status] IN (1, 2, 4)");
    }

    [Fact]
    public void La_clave_de_idempotencia_es_unica_por_miembro()
    {
        var i = Entidad<AnnualFeePayment>().GetIndexes()
            .Single(x => x.GetDatabaseName() == "UX_AnnualFeePayments_Member_IdempotencyKey");
        i.IsUnique.Should().BeTrue();
        i.GetFilter().Should().Be("[IdempotencyKey] IS NOT NULL");
    }

    [Fact]
    public void Los_ids_de_miembro_miden_al_menos_100()
    {
        Entidad<AnnualFeePayment>().FindProperty(nameof(AnnualFeePayment.MemberId))!.GetMaxLength()
            .Should().BeGreaterThanOrEqualTo(100);
        Entidad<SubscriptionBillingState>().FindProperty(nameof(SubscriptionBillingState.MemberId))!.GetMaxLength()
            .Should().BeGreaterThanOrEqualTo(100);
    }

    [Fact]
    public void Los_estados_vivos_del_filtro_coinciden_con_el_enumerado()
    {
        ((int)AnnualFeePaymentStatus.Pending).Should().Be(1);
        ((int)AnnualFeePaymentStatus.Completed).Should().Be(2);
        ((int)AnnualFeePaymentStatus.Failed).Should().Be(3);
        ((int)AnnualFeePaymentStatus.NeedsReconciliation).Should().Be(4);
    }

    // ── Transiciones del pago ──────────────────────────────────────────────────

    private static readonly DateTime T = new(2027, 6, 10, 9, 0, 0);

    [Fact]
    public void Un_pago_pendiente_se_completa_y_queda_vivo()
    {
        var p = new AnnualFeePayment();
        p.Complete("ph-1", T, "prueba");
        p.Status.Should().Be(AnnualFeePaymentStatus.Completed);
        p.PaymentHistoryId.Should().Be("ph-1");
        p.CompletedAt.Should().Be(T);
        p.IsLive.Should().BeTrue();
    }

    [Fact]
    public void Con_token_guarda_el_codigo_y_la_instancia()
    {
        var p = new AnnualFeePayment();
        p.CompleteWithToken(42, "ABCD-1234", T, "prueba");
        p.TokenTransactionId.Should().Be(42);
        p.TokenCode.Should().Be("ABCD-1234");
        p.Status.Should().Be(AnnualFeePaymentStatus.Completed);
    }

    [Fact]
    public void Un_pago_fallido_deja_de_estar_vivo_y_no_se_completa()
    {
        var p = new AnnualFeePayment();
        p.Fail("EWALLET_DEBIT_REJECTED", T, "prueba");
        p.IsLive.Should().BeFalse();
        var act = () => p.Complete("ph-1", T, "prueba");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Solo_un_pendiente_pasa_a_conciliar_y_un_conciliado_se_puede_cerrar()
    {
        var p = new AnnualFeePayment();
        p.MarkForReconciliation("EWALLET_OUTCOME_UNKNOWN", new string('x', 900), T, "prueba");
        p.Status.Should().Be(AnnualFeePaymentStatus.NeedsReconciliation);
        p.Notes!.Length.Should().Be(AnnualFeePayment.NotesMaxLength);
        var otraVez = () => p.MarkForReconciliation("X", null, T, "prueba");
        otraVez.Should().Throw<InvalidOperationException>();
        p.Complete("ph-2", T, "tesoreria");
        p.Status.Should().Be(AnnualFeePaymentStatus.Completed);
    }
}
