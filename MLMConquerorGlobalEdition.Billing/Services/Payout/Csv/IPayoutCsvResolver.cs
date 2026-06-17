using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Csv;

public interface IPayoutCsvResolver
{
    Result<IPayoutCsvFormatter> ResolveFormatter(WalletType walletType);
    Result<IPayoutResultCsvParser> ResolveParser(WalletType walletType);
}
