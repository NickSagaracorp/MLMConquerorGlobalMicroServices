using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Wallet;

public class MemberCreditCard : AuditChangesStringKey
{
    public string MemberId { get; set; } = string.Empty;         // "AMB-000001"
    public string MaskedCardNumber { get; set; } = string.Empty; // "411111******1111"
    public string Last4 { get; set; } = string.Empty;
    public string First6 { get; set; } = string.Empty;           // BIN for brand detection
    public string CardBrand { get; set; } = string.Empty;        // Visa, Mastercard, Amex…
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
    public string Gateway { get; set; } = string.Empty;          // "braintree" | "stripe"
    public string GatewayToken { get; set; } = string.Empty;     // gateway payment-method token
    public string CardToken { get; set; } = string.Empty;        // full tokenized card reference
    public bool IsExpired { get; set; }
    public bool IsDefault { get; set; }
}
