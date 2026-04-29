using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Wallet;

public class MemberCreditCard : AuditChangesStringKey
{
    public string MemberId { get; set; } = string.Empty;         // "AMB-000001"
    public string MaskedCardNumber { get; set; } = string.Empty; // "************1111" (last 4 only)
    public string Last4 { get; set; } = string.Empty;
    public string First6 { get; set; } = string.Empty;           // BIN for brand detection display

    /// <summary>
    /// First 12 digits of the PAN, encrypted (ENC:...). Never logged, never returned
    /// to the client. Allows future re-tokenization or fraud-team lookups.
    /// </summary>
    public string EncryptedPan { get; set; } = string.Empty;

    public string CardBrand { get; set; } = string.Empty;        // Visa, Mastercard, Amex…

    /// <summary>
    /// Card expiration encrypted as "MM/YYYY" (ENC:...). Decrypted server-side
    /// for display in the wallet UI; never stored in plain text.
    /// </summary>
    public string EncryptedExpiry { get; set; } = string.Empty;

    /// <summary>
    /// CVV/CVC encrypted (ENC:...). NEVER decrypted for display — the UI always
    /// shows it as "***". In a real PCI-compliant integration this column stays
    /// null because the gateway (Spreedly/Stripe) processes and discards the CVV.
    /// Stored only because the current dev setup simulates the gateway end-to-end.
    /// </summary>
    public string? EncryptedCvv { get; set; }
    public string Gateway { get; set; } = string.Empty;          // "spreedly" | "stripe"
    public string GatewayToken { get; set; } = string.Empty;     // gateway payment-method token
    public string CardToken { get; set; } = string.Empty;        // full tokenized card reference
    public bool IsExpired { get; set; }
    public bool IsDefault { get; set; }

    /// <summary>
    /// Order in which this card is tried when charging the member's recurring fees.
    /// Lower = earlier. Tied to drag-and-drop on the BizCenter Payment Method tab.
    /// </summary>
    public int Priority { get; set; }
}
