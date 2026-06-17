using MediatR;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.MarkPayoutBatchPaid;

public class MarkPayoutBatchPaidHandler
    : IRequestHandler<MarkPayoutBatchPaidCommand, Result<BatchReconcileResult>>
{
    private readonly IPayoutBatchReconciliationService _reconciliation;

    public MarkPayoutBatchPaidHandler(IPayoutBatchReconciliationService reconciliation)
        => _reconciliation = reconciliation;

    public Task<Result<BatchReconcileResult>> Handle(
        MarkPayoutBatchPaidCommand request, CancellationToken ct)
        => _reconciliation.MarkBatchPaidAsync(request.BatchId, ct);
}
