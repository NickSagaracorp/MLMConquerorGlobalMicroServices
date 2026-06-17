using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout;

public class PayoutReconciliationService : IPayoutReconciliationService
{
    /// <summary>
    /// Grace period before a Pending Online attempt is considered stuck. Must comfortably exceed
    /// the worst-case duration of <see cref="PayoutOrchestrator.ExecutePayoutAsync"/> so an in-flight
    /// attempt is never reconciled out from under itself.
    /// </summary>
    private const int StaleThresholdMinutes = 15;

    private const string ActorName = "payout-reconciliation-job";

    private readonly AppDbContext _db;
    private readonly IPayoutGatewayResolver _resolver;
    private readonly IPayoutOrchestrator _orchestrator;
    private readonly IDateTimeProvider _dateTime;

    public PayoutReconciliationService(
        AppDbContext db,
        IPayoutGatewayResolver resolver,
        IPayoutOrchestrator orchestrator,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _resolver = resolver;
        _orchestrator = orchestrator;
        _dateTime = dateTime;
    }

    public async Task<PayoutReconciliationSummary> ReconcileStalePayoutsAsync(CancellationToken ct = default)
    {
        var now = _dateTime.Now;
        var cutoff = now.AddMinutes(-StaleThresholdMinutes);

        // Only Online attempts: CsvBulk attempts are reserved by a PayoutBatch and reconciled
        // by PayoutBatchReconciliationService against the provider's result file, not here.
        var stale = await _db.PayoutAttempts
            .Where(a => a.Outcome == PayoutOutcome.Pending
                        && a.DisbursementMode == DisbursementMode.Online
                        && a.AttemptedAtUtc < cutoff)
            .OrderBy(a => a.AttemptedAtUtc)
            .ToListAsync(ct);

        var summary = new PayoutReconciliationSummary { Scanned = stale.Count };

        foreach (var attempt in stale)
        {
            var gatewayResult = _resolver.Resolve(attempt.WalletTypeSnapshot);
            if (!gatewayResult.IsSuccess)
            {
                summary.Unresolved++;
                continue;
            }

            var statusResult = await gatewayResult.Value!.GetTransferStatusAsync(attempt.Id.ToString(), ct);
            if (!statusResult.IsSuccess)
            {
                summary.Unresolved++;
                continue;
            }

            switch (statusResult.Value!.State)
            {
                case PayoutTransferState.Succeeded:
                    // Money left but the local commit was lost — finish the bookkeeping.
                    await _orchestrator.FinalizeSuccessAsync(
                        attempt, statusResult.Value.GatewayTransactionId, latencyMs: null, ct);
                    summary.Recovered++;
                    break;

                case PayoutTransferState.Failed:
                case PayoutTransferState.NotFound:
                    // Money never left — fail the attempt, which releases the reserved earnings
                    // (the reservation query excludes Failed attempts) so they re-enter candidacy.
                    await MarkFailedAndReleaseAsync(attempt, statusResult.Value, now, ct);
                    summary.Released++;
                    break;

                default: // Unknown — gateway indeterminate; leave Pending and retry next sweep.
                    summary.Unresolved++;
                    break;
            }
        }

        return summary;
    }

    private async Task MarkFailedAndReleaseAsync(
        PayoutAttempt attempt, PayoutTransferStatusResult status, DateTime now, CancellationToken ct)
    {
        attempt.Outcome = PayoutOutcome.Failed;
        attempt.GatewayErrorCode = status.GatewayCode ?? "RECONCILED_NOT_SENT";
        attempt.GatewayErrorMessage = status.GatewayMessage
            ?? "Reconciliation: gateway reports no successful transfer; earnings released.";
        attempt.CompletedAtUtc = now;
        attempt.LastUpdateDate = now;
        attempt.LastUpdateBy = ActorName;
        await _db.SaveChangesAsync(ct);
    }
}
