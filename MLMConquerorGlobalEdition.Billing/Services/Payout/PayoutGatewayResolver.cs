using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout;

public class PayoutGatewayResolver : IPayoutGatewayResolver
{
    private readonly IEnumerable<IPayoutGatewayService> _gateways;

    public PayoutGatewayResolver(IEnumerable<IPayoutGatewayService> gateways) => _gateways = gateways;

    public Result<IPayoutGatewayService> Resolve(WalletType walletType)
    {
        var gateway = _gateways.FirstOrDefault(g => g.GatewayType == walletType);
        return gateway is null
            ? Result<IPayoutGatewayService>.Failure(
                "PAYOUT_GATEWAY_NOT_SUPPORTED", $"No payout gateway configured for {walletType}")
            : Result<IPayoutGatewayService>.Success(gateway);
    }
}
