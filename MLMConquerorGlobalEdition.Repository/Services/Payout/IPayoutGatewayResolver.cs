using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Payout;

public interface IPayoutGatewayResolver
{
    Result<IPayoutGatewayService> Resolve(WalletType walletType);
}
