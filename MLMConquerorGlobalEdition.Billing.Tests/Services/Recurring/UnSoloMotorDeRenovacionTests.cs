using FluentAssertions;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Plan K (decisión de Nick): un solo motor de renovación. MembershipAutoRenewalJob renovaba por su
/// cuenta lo mismo que cobra RecurringBillingProcessor, sin candado ni idempotencia: con los dos, una
/// suscripción podía cobrarse dos veces. Se queda el procesador (planificador + barrido).
/// </summary>
public class UnSoloMotorDeRenovacionTests
{
    private static readonly string Programa = Repositorio.Leer("MLMConquerorGlobalEdition.Billing", "Program.cs");

    [Fact]
    public void MembershipAutoRenewalJob_no_se_programa()
        => Programa.Should().NotContain("AddOrUpdate<MembershipAutoRenewalJob>");

    [Fact]
    public void Se_borra_de_Hangfire_si_un_despliegue_anterior_lo_dejo_programado()
        => Programa.Should().Contain("RecurringJob.RemoveIfExists(\"membership-auto-renewal\");");

    [Fact]
    public void El_motor_que_se_queda_sigue_programado()
    {
        Programa.Should().Contain("AddOrUpdate<RecurringBillingPlanningJob>");
        Programa.Should().Contain("AddOrUpdate<RecurringBillingSweepJob>");
    }
}
