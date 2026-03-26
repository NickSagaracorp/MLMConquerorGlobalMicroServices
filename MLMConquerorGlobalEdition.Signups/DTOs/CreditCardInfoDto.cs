namespace MLMConquerorGlobalEdition.Signups.DTOs;

/// <summary>
/// Credit card data captured during checkout.
/// Raw card number is never stored — only the gateway token and masked/partial data.
/// </summary>
public class CreditCardInfoDto
{
    /// <summary>Gateway tokenized card reference (e.g., Braintree nonce or Stripe PaymentMethod ID).</summary>
    public string GatewayToken { get; set; } = string.Empty;

    /// <summary>Additional card token from the gateway (full payment method token).</summary>
    public string CardToken { get; set; } = string.Empty;

    /// <summary>Last 4 digits of the card.</summary>
    public string Last4 { get; set; } = string.Empty;

    /// <summary>First 6 digits (BIN) for card brand detection.</summary>
    public string First6 { get; set; } = string.Empty;

    /// <summary>Card brand: Visa, Mastercard, Amex, etc.</summary>
    public string CardBrand { get; set; } = string.Empty;

    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }

    /// <summary>Payment gateway used: "braintree" | "stripe".</summary>
    public string Gateway { get; set; } = string.Empty;
}
