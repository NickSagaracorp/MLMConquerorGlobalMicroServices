using MediatR;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ExportPayoutBatch;

public record ExportPayoutBatchCommand(WalletType WalletType, DateTime ProcessDate)
    : IRequest<Result<PayoutBatchExportResult>>;
