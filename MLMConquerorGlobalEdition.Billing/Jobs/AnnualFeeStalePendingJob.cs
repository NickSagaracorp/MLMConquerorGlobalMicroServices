using Hangfire;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// Cada hora: pasa a conciliar los pagos de cuota que se quedaron Pending (el proceso murió entre el
/// débito de i-Payout y su registro). Nunca los libera: el dinero pudo moverse (Plan K, K11).
/// </summary>
[Queue("billing")]
public class AnnualFeeStalePendingJob
{
    private const string Actor = "annual-fee-stale-sweep";

    private readonly IAnnualFeeChargeService _service;
    private readonly IConfiguration _config;
    private readonly ILogger<AnnualFeeStalePendingJob> _logger;

    public AnnualFeeStalePendingJob(IAnnualFeeChargeService service, IConfiguration config, ILogger<AnnualFeeStalePendingJob> logger)
    {
        _service = service;
        _config  = config;
        _logger  = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var minutes = _config.GetValue(AnnualFeeRules.StalePendingMinutesConfigKey, AnnualFeeRules.DefaultStalePendingMinutes);
        var moved   = await _service.MoveStalePendingToReconciliationAsync(minutes, Actor, ct);
        if (moved > 0)
            _logger.LogWarning("AnnualFeeStalePendingJob: {Moved} pagos de cuota pasaron a conciliar.", moved);
    }
}
