using MediatR;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ReconcilePayoutBatch;

public record ReconcilePayoutBatchCommand(string BatchId, string ResultCsv)
    : IRequest<Result<BatchReconcileResult>>;
