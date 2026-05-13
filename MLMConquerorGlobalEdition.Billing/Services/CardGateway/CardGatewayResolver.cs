using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Services.CardGateway;

/// <summary>
/// Maps CardProcessor → ICardGatewayService.
///
/// Per BILLING-RULES §3, ALL processors are handled by the single SpreedlyCardGatewayService.
/// Spreedly is the universal vault; the downstream processor is selected by our routing engine
/// and passed via GatewayChargeRequest.DownstreamProcessor so Spreedly knows which gateway to use.
/// </summary>
public class CardGatewayResolver : ICardGatewayResolver
{
    private readonly SpreedlyCardGatewayService _spreedly;

    public CardGatewayResolver(SpreedlyCardGatewayService spreedly)
    {
        _spreedly = spreedly;
    }

    /// <summary>
    /// Returns the SpreedlyCardGatewayService for every CardProcessor value.
    /// The caller must set GatewayChargeRequest.DownstreamProcessor before calling ChargeAsync
    /// so the service knows which Spreedly downstream-gateway-token to look up.
    /// </summary>
    public ICardGatewayService Resolve(CardProcessor processor) => _spreedly;
}

public interface ICardGatewayResolver
{
    ICardGatewayService Resolve(CardProcessor processor);
}
