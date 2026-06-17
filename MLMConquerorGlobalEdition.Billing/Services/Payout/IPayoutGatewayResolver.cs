using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout;

public interface IPayoutGatewayResolver
{
    Result<IPayoutGatewayService> Resolve(WalletType walletType);
}
