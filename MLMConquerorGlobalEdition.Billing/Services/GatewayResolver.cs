using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Services;

public class GatewayResolver : IGatewayResolver
{
    private readonly Dictionary<WalletType, IGatewayService> _gateways;

    public GatewayResolver(IEnumerable<IGatewayService> gateways)
    {
        _gateways = gateways.ToDictionary(g => g.GatewayType);
    }

    public IGatewayService Resolve(WalletType type)
    {
        if (_gateways.TryGetValue(type, out var gateway))
            return gateway;

        throw new InvalidOperationException($"No gateway registered for WalletType '{type}'.");
    }

    public IEnumerable<WalletType> AvailableGateways => _gateways.Keys;
}
