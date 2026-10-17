using FluentAssertions;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// Las exclusiones del legado (Plan K, K5) en el orden de BizCenterFeeMWRLife.BillMonthlyPayment:
/// cancelada, menos de un año, suspendida, membresía en HoldByBilling (Nick, 2026-10-17), cobrada hace
/// poco y KeepQualified. Y lo que pide
/// "Pagar ahora" (K14).
/// </summary>
public class AnnualFeeEligibilityTests
{
    private static readonly DateTime Ahora = new(2027, 6, 10, 9, 0, 0);
    private static readonly DateTime Vence = new(2027, 6, 5);

    private static AnnualFeeMemberFacts Miembro(
        MemberAccountStatus estado = MemberAccountStatus.Active, DateTime? alta = null, bool mantener = false,
        MemberType tipo = MemberType.Ambassador, bool enEspera = false)
        => new(true, tipo, estado, alta ?? new DateTime(2026, 6, 5), mantener, enEspera);

    private static AnnualFeeDecision Automatico(AnnualFeeMemberFacts m, DateTime? vence = null, DateTime? ultimo = null, bool forzado = false)
        => AnnualFeeEligibility.ForAutomaticCharge(m, vence ?? Vence, ultimo, Ahora, forzado);

    [Fact]
    public void Vencida_y_activa_se_cobra()
        => Automatico(Miembro()).Kind.Should().Be(AnnualFeeDecisionKind.Charge);

    [Fact]
    public void Inactiva_tambien_se_cobra()
        => Automatico(Miembro(MemberAccountStatus.Inactive)).Kind.Should().Be(AnnualFeeDecisionKind.Charge);

    [Fact]
    public void Terminada_para_el_estado_para_siempre()
    {
        var d = Automatico(Miembro(MemberAccountStatus.Terminated));
        d.Kind.Should().Be(AnnualFeeDecisionKind.Stop);
        d.Reason.Should().Be(AnnualFeeEligibility.ReasonAccountClosed);
    }

    [Fact]
    public void Sin_perfil_o_si_ya_no_es_Ambassador_tambien_se_para()
    {
        Automatico(AnnualFeeMemberFacts.Missing).Kind.Should().Be(AnnualFeeDecisionKind.Stop);
        Automatico(Miembro(tipo: MemberType.ExternalMember)).Kind.Should().Be(AnnualFeeDecisionKind.Stop);
    }

    [Fact]
    public void Con_menos_de_un_anio_se_reprograma_a_su_aniversario()
    {
        var d = Automatico(Miembro(alta: new DateTime(2026, 12, 1, 15, 0, 0)), vence: new DateTime(2027, 6, 1));
        d.Kind.Should().Be(AnnualFeeDecisionKind.Reschedule);
        d.Reason.Should().Be(AnnualFeeEligibility.ReasonUnderOneYear);
        d.NextDue.Should().Be(new DateTime(2027, 12, 1));
    }

    [Theory]
    [InlineData(MemberAccountStatus.Suspended, AnnualFeeEligibility.ReasonSuspended)]
    [InlineData(MemberAccountStatus.Pending, AnnualFeeEligibility.ReasonAccountPending)]
    public void Suspendida_o_pendiente_se_mira_otra_vez_manana(MemberAccountStatus estado, string razon)
    {
        var d = Automatico(Miembro(estado));
        d.Kind.Should().Be(AnnualFeeDecisionKind.Postpone);
        d.Reason.Should().Be(razon);
    }

    [Fact]
    public void Cobrada_hace_menos_de_11_meses_se_reprograma_a_un_anio_del_cobro()
    {
        var d = Automatico(Miembro(), ultimo: new DateTime(2027, 1, 10, 8, 0, 0));
        d.Kind.Should().Be(AnnualFeeDecisionKind.Reschedule);
        d.Reason.Should().Be(AnnualFeeEligibility.ReasonRecentlyBilled);
        d.NextDue.Should().Be(new DateTime(2028, 1, 10));
    }

    [Fact]
    public void Cobrada_hace_mas_de_11_meses_se_cobra()
        => Automatico(Miembro(), ultimo: new DateTime(2026, 6, 5)).Kind.Should().Be(AnnualFeeDecisionKind.Charge);

    [Fact]
    public void KeepQualified_no_se_cobra_ni_avanza_y_se_mira_manana()
    {
        var d = Automatico(Miembro(mantener: true));
        d.Kind.Should().Be(AnnualFeeDecisionKind.Postpone);
        d.Reason.Should().Be(AnnualFeeEligibility.ReasonKeepQualified);
        d.NextDue.Should().BeNull();
    }

    [Fact]
    public void Si_aun_no_vence_se_reprograma_a_su_vencimiento()
    {
        var d = Automatico(Miembro(), vence: new DateTime(2027, 6, 20));
        d.Kind.Should().Be(AnnualFeeDecisionKind.Reschedule);
        d.Reason.Should().Be(AnnualFeeEligibility.ReasonNotDue);
        d.NextDue.Should().Be(new DateTime(2027, 6, 20));
    }

    [Fact]
    public void Forzado_se_cobra_aunque_no_venza()
        => Automatico(Miembro(), vence: new DateTime(2027, 6, 20), forzado: true).Kind.Should().Be(AnnualFeeDecisionKind.Charge);

    [Fact]
    public void Forzado_no_se_salta_las_exclusiones()
        => Automatico(Miembro(MemberAccountStatus.Suspended), forzado: true).Kind.Should().Be(AnnualFeeDecisionKind.Postpone);

    [Fact]
    public void El_orden_es_el_del_legado_cancelada_antes_que_KeepQualified()
        => Automatico(Miembro(MemberAccountStatus.Terminated, mantener: true)).Kind.Should().Be(AnnualFeeDecisionKind.Stop);

    [Fact]
    public void El_orden_es_el_del_legado_menos_de_un_anio_antes_que_suspendida()
        => Automatico(Miembro(MemberAccountStatus.Suspended, alta: new DateTime(2026, 12, 1)), vence: new DateTime(2027, 12, 1))
            .Reason.Should().Be(AnnualFeeEligibility.ReasonUnderOneYear);

    [Fact]
    public void Los_hechos_salen_del_perfil_y_un_perfil_borrado_es_como_no_tenerlo()
    {
        var perfil = new MemberProfile
        {
            MemberType = MemberType.Ambassador, Status = MemberAccountStatus.Active,
            EnrollDate = new DateTime(2026, 6, 5), KeepQualified = true
        };
        AnnualFeeMemberFacts.Of(perfil).Should().Be(new AnnualFeeMemberFacts(true, MemberType.Ambassador,
            MemberAccountStatus.Active, new DateTime(2026, 6, 5), true));

        perfil.IsDeleted = true;
        AnnualFeeMemberFacts.Of(perfil).Exists.Should().BeFalse();
        AnnualFeeMemberFacts.Of(null).Exists.Should().BeFalse();
    }

    // ── Pagar ahora ──────────────────────────────────────────────────────────

    [Fact]
    public void Pagar_ahora_vale_con_la_cuenta_activa_y_la_cuota_vencida()
        => AnnualFeeEligibility.ForPayNow(Miembro(), Vence, Ahora).Should().BeNull();

    [Fact]
    public void Pagar_ahora_vale_hasta_30_dias_antes()
    {
        AnnualFeeEligibility.ForPayNow(Miembro(), Ahora.Date.AddDays(30), Ahora).Should().BeNull();
        AnnualFeeEligibility.ForPayNow(Miembro(), Ahora.Date.AddDays(31), Ahora).Should().Be(AnnualFeeErrors.NotDue);
    }

    [Theory]
    [InlineData(MemberAccountStatus.Inactive)]
    [InlineData(MemberAccountStatus.Suspended)]
    [InlineData(MemberAccountStatus.Pending)]
    [InlineData(MemberAccountStatus.Terminated)]
    public void Pagar_ahora_exige_la_cuenta_activa(MemberAccountStatus estado)
        => AnnualFeeEligibility.ForPayNow(Miembro(estado), Vence, Ahora).Should().Be(AnnualFeeErrors.AccountNotActive);

    [Fact]
    public void Con_KeepQualified_no_hay_cuota_que_pagar()
        => AnnualFeeEligibility.ForPayNow(Miembro(mantener: true), Vence, Ahora).Should().Be(AnnualFeeErrors.Waived);

    [Fact]
    public void Un_cliente_no_tiene_cuota()
        => AnnualFeeEligibility.ForPayNow(Miembro(tipo: MemberType.ExternalMember), Vence, Ahora).Should().Be(AnnualFeeErrors.NotApplicable);

    // ── Membresía en HoldByBilling (decisión de Nick, 2026-10-17) ─────────────

    [Fact]
    public void Con_la_membresia_en_HoldByBilling_no_se_cobra_ni_avanza_y_se_mira_manana()
    {
        var d = Automatico(Miembro(enEspera: true));
        d.Kind.Should().Be(AnnualFeeDecisionKind.Postpone);
        d.Reason.Should().Be(AnnualFeeEligibility.ReasonMembershipOnHold);
        d.NextDue.Should().BeNull();
    }

    [Fact]
    public void Forzado_tampoco_cobra_con_la_membresia_en_HoldByBilling()
        => Automatico(Miembro(enEspera: true), forzado: true).Kind.Should().Be(AnnualFeeDecisionKind.Postpone);

    [Fact]
    public void HoldByBilling_va_despues_de_cancelada_menos_de_un_anio_y_suspendida()
    {
        Automatico(Miembro(MemberAccountStatus.Terminated, enEspera: true)).Reason.Should().Be(AnnualFeeEligibility.ReasonAccountClosed);
        Automatico(Miembro(alta: new DateTime(2026, 12, 1), enEspera: true), vence: new DateTime(2027, 12, 1))
            .Reason.Should().Be(AnnualFeeEligibility.ReasonUnderOneYear);
        Automatico(Miembro(MemberAccountStatus.Suspended, enEspera: true)).Reason.Should().Be(AnnualFeeEligibility.ReasonSuspended);
    }

    [Fact]
    public void HoldByBilling_va_antes_que_cobrada_hace_poco_y_KeepQualified()
    {
        Automatico(Miembro(enEspera: true), ultimo: new DateTime(2027, 1, 10)).Reason.Should().Be(AnnualFeeEligibility.ReasonMembershipOnHold);
        Automatico(Miembro(mantener: true, enEspera: true)).Reason.Should().Be(AnnualFeeEligibility.ReasonMembershipOnHold);
    }

    [Fact]
    public void Pagar_ahora_no_vale_con_la_membresia_en_HoldByBilling()
        => AnnualFeeEligibility.ForPayNow(Miembro(enEspera: true), Vence, Ahora).Should().Be(AnnualFeeErrors.MembershipOnHold);

    [Fact]
    public void Pagar_ahora_con_la_cuenta_inactiva_y_en_espera_dice_primero_la_cuenta()
        => AnnualFeeEligibility.ForPayNow(Miembro(MemberAccountStatus.Inactive, enEspera: true), Vence, Ahora)
            .Should().Be(AnnualFeeErrors.AccountNotActive);

    [Fact]
    public void Los_hechos_llevan_la_espera_de_la_membresia()
        => AnnualFeeMemberFacts.Of(new MemberProfile { MemberType = MemberType.Ambassador }, membershipOnHold: true)
            .MembershipOnHold.Should().BeTrue();
}
