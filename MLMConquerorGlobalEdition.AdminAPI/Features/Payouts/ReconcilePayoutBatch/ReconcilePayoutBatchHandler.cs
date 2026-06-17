using MediatR;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ReconcilePayoutBatch;

public class ReconcilePayoutBatchHandler
    : IRequestHandler<ReconcilePayoutBatchCommand, Result<BatchReconcileResult>>
{
    private readonly IPayoutBatchReconciliationService _reconciliation;

    public ReconcilePayoutBatchHandler(IPayoutBatchReconciliationService reconciliation)
        => _reconciliation = reconciliation;

    public Task<Result<BatchReconcileResult>> Handle(
        ReconcilePayoutBatchCommand request, CancellationToken ct)
        => _reconciliation.ReconcileFromResultsAsync(request.BatchId, request.ResultCsv, ct);
}
