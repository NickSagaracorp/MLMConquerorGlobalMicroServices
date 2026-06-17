using MediatR;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.CancelPayoutBatch;

public class CancelPayoutBatchHandler
    : IRequestHandler<CancelPayoutBatchCommand, Result<BatchReconcileResult>>
{
    private readonly IPayoutBatchReconciliationService _reconciliation;

    public CancelPayoutBatchHandler(IPayoutBatchReconciliationService reconciliation)
        => _reconciliation = reconciliation;

    public Task<Result<BatchReconcileResult>> Handle(
        CancelPayoutBatchCommand request, CancellationToken ct)
        => _reconciliation.CancelBatchAsync(request.BatchId, ct);
}
