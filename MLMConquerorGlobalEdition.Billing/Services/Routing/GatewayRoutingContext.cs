using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Services.Routing;

/// <summary>
/// Input context for the gateway routing decision.
/// </summary>
public class GatewayRoutingContext
{
    public BillingOperationType OperationType        { get; init; }
    public CardBrand             CardBrand            { get; init; }
    public string                CardholderCountryIso { get; init; } = string.Empty;
    public decimal               AmountUsd            { get; init; }
    public string                MemberId             { get; init; } = string.Empty;

    /// <summary>When set, the router is bypassed and this processor is used directly.</summary>
    public CardProcessor?        AdminOverride        { get; init; }
}
