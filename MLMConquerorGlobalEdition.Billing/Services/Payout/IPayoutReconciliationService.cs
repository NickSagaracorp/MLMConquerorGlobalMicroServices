namespace MLMConquerorGlobalEdition.Billing.Services.Payout;

/// <summary>
/// Resolves online payout attempts left stuck in <c>Pending</c> by a crash between the gateway
/// disburse and the local SaveChanges. For each stale attempt it asks the gateway whether the
/// money actually left (never guesses), then finalizes (money sent) or releases the reserved
/// earnings (money not sent). Idempotent: only touches stale Pending Online attempts.
/// </summary>
public interface IPayoutReconciliationService
{
    Task<PayoutReconciliationSummary> ReconcileStalePayoutsAsync(CancellationToken ct = default);
}

/// <summary>Outcome counts for one reconciliation sweep.</summary>
public class PayoutReconciliationSummary
{
    /// <summary>Stale Pending Online attempts examined this sweep.</summary>
    public int Scanned { get; set; }
    /// <summary>Attempts the gateway confirmed Succeeded → finalized (earnings marked Paid).</summary>
    public int Recovered { get; set; }
    /// <summary>Attempts the gateway reported Failed/NotFound → marked Failed (earnings released).</summary>
    public int Released { get; set; }
    /// <summary>Attempts left Pending (gateway unreachable/Unknown, or no gateway) — retried next sweep.</summary>
    public int Unresolved { get; set; }
}
