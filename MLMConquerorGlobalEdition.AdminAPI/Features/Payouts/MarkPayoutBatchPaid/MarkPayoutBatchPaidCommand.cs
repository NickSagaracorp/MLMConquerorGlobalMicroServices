using MediatR;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.MarkPayoutBatchPaid;

public record MarkPayoutBatchPaidCommand(string BatchId)
    : IRequest<Result<BatchReconcileResult>>;
