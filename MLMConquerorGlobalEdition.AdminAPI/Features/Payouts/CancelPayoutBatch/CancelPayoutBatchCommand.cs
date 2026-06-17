using MediatR;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.CancelPayoutBatch;

public record CancelPayoutBatchCommand(string BatchId)
    : IRequest<Result<BatchReconcileResult>>;
