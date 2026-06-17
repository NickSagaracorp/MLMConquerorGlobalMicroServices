using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout;

public interface IPayoutOrchestrator
{
    /// <summary>
    /// Pays a single ambassador via their preferred gateway: lazy validate→subscribe→disburse,
    /// writes an immutable PayoutAttempt + per-call MemberWalletApiLog, and on gateway success
    /// marks the member's pending earnings (PaymentDate ≤ processDate) Paid. On failure the
    /// earnings stay Pending and the gateway error is audited.
    /// </summary>
    Task<Result<PayoutResult>> ExecutePayoutAsync(string memberId, DateTime processDate, CancellationToken ct = default);

    /// <summary>Marks an attempt's reserved earnings Paid, sets the attempt Success, and issues the
    /// receipt (best-effort). Shared by the online path and CSV reconciliation. Computes timestamp/actor
    /// internally. gatewayTxnId/latencyMs are null for CSV-bulk reconciliation.</summary>
    Task FinalizeSuccessAsync(PayoutAttempt attempt,
        string? gatewayTxnId, long? latencyMs, CancellationToken ct = default);
}
