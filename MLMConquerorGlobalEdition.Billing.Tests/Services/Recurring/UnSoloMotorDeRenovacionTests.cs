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
    public void El_alta_de_estados_de_cuota_anual_se_registra_apagada_salvo_que_se_encienda()
    {
        // Plan K (Tarea 8, revisada): sin la clave en true se programa con Cron.Never(): sale en el panel
        // de Hangfire para lanzarlo a mano, pero no corre al arrancar ni de madrugada.
        Programa.Should().Contain("\"Billing:AnnualFee:EnrollmentJobEnabled\"");
        Programa.Should().Contain("AddOrUpdate<AnnualFeeStateEnrollmentJob>");
        Programa.Should().Contain("Cron.Never()");
        Programa.Should().NotContain("Enqueue<AnnualFeeStateEnrollmentJob>");
        Programa.Should().NotContain("TriggerJob(\"annual-fee-state-enrollment\")");
    }

    [Fact]
    public void Ningun_appsettings_enciende_el_alta_de_estados()
    {
        foreach (var f in Directory.GetFiles(Path.Combine(Repositorio.Raiz(), "MLMConquerorGlobalEdition.Billing"), "appsettings*.json"))
            File.ReadAllText(f).Should().NotContain("EnrollmentJobEnabled", because: f);
    }

    [Fact]
    public void El_motor_que_se_queda_sigue_programado()
    {
        Programa.Should().Contain("AddOrUpdate<RecurringBillingPlanningJob>");
        Programa.Should().Contain("AddOrUpdate<RecurringBillingSweepJob>");
    }
}
