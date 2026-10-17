using Hangfire;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// Crea los estados de cuota anual que falten (Plan K, K3). Se registra APAGADO (Cron.Never) salvo que
/// Billing:AnnualFee:EnrollmentJobEnabled sea true; entonces, diario a las 4:30 UTC, antes del
/// planificador (5:00). Tras el primer despliegue se lanza a mano desde el panel de Hangfire ("Trigger now").
/// </summary>
[Queue("billing")]
public class AnnualFeeStateEnrollmentJob
{
    private readonly IAnnualFeeStateEnrollment _enrollment;
    private readonly ILogger<AnnualFeeStateEnrollmentJob> _logger;

    public AnnualFeeStateEnrollmentJob(IAnnualFeeStateEnrollment enrollment, ILogger<AnnualFeeStateEnrollmentJob> logger)
    {
        _enrollment = enrollment;
        _logger     = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var created = await _enrollment.EnsureMissingAsync(
            AnnualFeeRules.EnrollmentBatchSize, AnnualFeeRules.EnrollmentMaxBatchesPerRun, ct);
        _logger.LogInformation("AnnualFeeStateEnrollmentJob: {Created} estados de cuota anual creados.", created);
    }
}
