namespace MLMConquerorGlobalEdition.SharedAPICenter.DTOs;

/// <summary>
/// Inbound payload sent by payment providers (Stripe, Braintree, Crypto) to
/// the webhook endpoints. Covers both payment and refund event types.
/// </summary>
public class WebhookPaymentPayload
{
    /// <summary>
    /// Provider-specific event name, e.g. "payment.success", "payment.failed", "refund.created".
    /// </summary>
    public string Event { get; set; } = string.Empty;

    /// <summary>Gateway-issued unique transaction identifier.</summary>
    public string TransactionId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    /// <summary>Internal order ID, if the provider echoes it back.</summary>
    public string? OrderId { get; set; }

    /// <summary>Internal member ID, if the provider echoes it back.</summary>
    public string? MemberId { get; set; }

    /// <summary>Arbitrary provider metadata (gateway reference, customer email, etc.).</summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
