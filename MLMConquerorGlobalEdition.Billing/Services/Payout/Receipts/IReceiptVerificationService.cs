using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;

public interface IReceiptVerificationService
{
    /// <summary>Re-reads the stored PDF, recomputes its SHA-256 vs the recorded hash, re-walks the chain
    /// link, and reports anchor status.</summary>
    Task<ReceiptVerificationResult> VerifyAsync(PayoutAttempt attempt, CancellationToken ct = default);
}
