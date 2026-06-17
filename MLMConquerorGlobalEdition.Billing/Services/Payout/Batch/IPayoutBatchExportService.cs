using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;

public interface IPayoutBatchExportService
{
    Task<Result<PayoutBatchExportResult>> ExportAsync(WalletType walletType, DateTime processDate, CancellationToken ct = default);
}
