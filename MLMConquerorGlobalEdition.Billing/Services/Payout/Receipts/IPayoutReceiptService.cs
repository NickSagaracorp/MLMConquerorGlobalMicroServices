using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;

public interface IPayoutReceiptService
{
    /// <summary>Best-effort: render + store the receipt, persist Url/Sha256 on the attempt, and email it
    /// if the auto-send toggle is on. Never throws — a receipt failure must not undo a settled payout.</summary>
    Task IssueReceiptAsync(PayoutAttempt attempt, CancellationToken ct = default);

    /// <summary>Ensures a receipt exists (regenerating if missing) and re-sends the email regardless of toggle.</summary>
    Task<bool> ResendReceiptAsync(PayoutAttempt attempt, CancellationToken ct = default);
}
