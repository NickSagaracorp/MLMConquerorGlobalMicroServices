using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Services;

public interface IGatewayResolver
{
    IGatewayService Resolve(WalletType type);
    IEnumerable<WalletType> AvailableGateways { get; }
}
