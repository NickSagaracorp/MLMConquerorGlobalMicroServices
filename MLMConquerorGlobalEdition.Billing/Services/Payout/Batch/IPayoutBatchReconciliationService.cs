using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;

public interface IPayoutBatchReconciliationService
{
    Task<Result<BatchReconcileResult>> ReconcileFromResultsAsync(string batchId, string resultCsv, CancellationToken ct = default);
    Task<Result<BatchReconcileResult>> MarkBatchPaidAsync(string batchId, CancellationToken ct = default);
    Task<Result<BatchReconcileResult>> CancelBatchAsync(string batchId, CancellationToken ct = default);
}
