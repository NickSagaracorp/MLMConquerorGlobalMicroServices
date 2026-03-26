namespace MLMConquerorGlobalEdition.Signups.DTOs;

/// <summary>Phase 3 of the signup wizard — payment and completion.</summary>
public class CompleteSignupRequest
{
    public PaymentMethodType PaymentMethod { get; set; }

    /// <summary>Populated when PaymentMethod = CreditCard.</summary>
    public CreditCardInfoDto? CreditCard { get; set; }

    /// <summary>Populated when PaymentMethod = Crypto.</summary>
    public string? CryptoCurrency { get; set; }
    public string? CryptoTransactionId { get; set; }

    /// <summary>Populated when PaymentMethod = Token.</summary>
    public string? TokenCode { get; set; }

    /// <summary>Populated when PaymentMethod = DiscountCode.</summary>
    public string? DiscountCode { get; set; }

    /// <summary>Base64-encoded screenshot of the checkout screen (chargeback evidence).</summary>
    public string? CheckoutScreenshotBase64 { get; set; }

    /// <summary>MIME type of the screenshot image (e.g., "image/png").</summary>
    public string CheckoutScreenshotContentType { get; set; } = "image/png";
}
