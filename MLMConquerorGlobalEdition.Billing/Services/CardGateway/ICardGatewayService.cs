using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.CardGateway;

/// <summary>
/// Raw card details captured server-side at signup (no client-side tokenization).
/// Used only for the first charge on a new card — Spreedly vaults it in the same
/// call (retain_on_success) and returns a payment_method_token for future charges.
/// Never persisted; discarded after the charge call returns.
/// </summary>
public class RawCardDetails
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName  { get; init; } = string.Empty;
    public string Number    { get; init; } = string.Empty;
    public int    Month     { get; init; }
    public int    Year      { get; init; }
    public string Cvv       { get; init; } = string.Empty;
}

public class GatewayChargeRequest
{
    public string  MemberId             { get; init; } = string.Empty;
    public decimal Amount               { get; init; }
    public string  Currency             { get; init; } = "USD";
    public string  Description          { get; init; } = string.Empty;
    public string? TokenizedCardRef     { get; init; }
    public string? NetworkTransactionId { get; init; }
    public bool    IsRecurring          { get; init; }

    /// <summary>
    /// The member's Spreedly payment_method_token (from MemberCreditCard.SpreedlyPaymentMethodToken).
    /// Required by SpreedlyCardGatewayService. The routing engine populates this before
    /// passing the request to the gateway.
    /// </summary>
    public string? SpreedlyPaymentMethodToken { get; init; }

    /// <summary>
    /// Which downstream processor the routing engine has selected for this charge.
    /// SpreedlyCardGatewayService uses this to look up the correct Spreedly downstream-gateway-token.
    /// </summary>
    public CardProcessor DownstreamProcessor { get; init; }

    /// <summary>
    /// Raw card details for a first-time charge when no SpreedlyPaymentMethodToken exists yet.
    /// Mutually exclusive with SpreedlyPaymentMethodToken — if both are set, the token wins.
    /// </summary>
    public RawCardDetails? RawCard { get; init; }

    /// <summary>
    /// When charging via RawCard, whether Spreedly should vault the card (retain_on_success)
    /// so it can be reused for recurring billing. Always true for signup's first charge.
    /// </summary>
    public bool RetainOnSuccess { get; init; }
}

public class GatewayChargeResult
{
    public string  GatewayTransactionId { get; init; } = string.Empty;
    public string  Status               { get; init; } = string.Empty;
    public string? RawResponse          { get; init; }

    /// <summary>
    /// Populated only when Spreedly vaulted a NEW payment method as part of this charge
    /// (i.e. the request used RawCard + RetainOnSuccess). Callers should persist this onto
    /// MemberCreditCard.SpreedlyPaymentMethodToken for future recurring charges.
    /// </summary>
    public string? SpreedlyPaymentMethodToken { get; init; }
}

/// <summary>
/// Card-charge gateway abstraction (separate from the wallet/payout IGatewayService).
/// </summary>
public interface ICardGatewayService
{
    CardProcessor Processor { get; }

    Task<Result<GatewayChargeResult>> ChargeAsync(
        GatewayChargeRequest req,
        CancellationToken ct = default);

    Task<Result<bool>> RefundAsync(
        string gatewayTransactionId,
        decimal amount,
        CancellationToken ct = default);
}
