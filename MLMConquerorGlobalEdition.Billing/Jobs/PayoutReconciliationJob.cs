using Hangfire;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Billing.Services.Payout;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// HangFire recurring job — every 30 minutes. Safety net that resolves online payout attempts
/// stuck in Pending after a crash between the gateway disburse and the local commit, by asking
/// the gateway whether the money actually left. Idempotent and resumable: each run only touches
/// stale Pending Online attempts and re-running is harmless.
/// </summary>
[Queue("billing")]
public class PayoutReconciliationJob
{
    private readonly IPayoutReconciliationService _reconciliation;
    private readonly ILogger<PayoutReconciliationJob> _logger;

    public PayoutReconciliationJob(
        IPayoutReconciliationService reconciliation,
        ILogger<PayoutReconciliationJob> logger)
    {
        _reconciliation = reconciliation;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var summary = await _reconciliation.ReconcileStalePayoutsAsync(ct);

        if (summary.Scanned == 0)
        {
            _logger.LogInformation("PayoutReconciliationJob: no stale pending payouts.");
            return;
        }

        _logger.LogInformation(
            "PayoutReconciliationJob: scanned {Scanned}, recovered {Recovered}, released {Released}, unresolved {Unresolved}.",
            summary.Scanned, summary.Recovered, summary.Released, summary.Unresolved);
    }
}
