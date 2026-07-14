namespace MLMConquerorGlobalEdition.SignupAPI.DTOs;

/// <summary>
/// Raw credit card data captured during checkout. SignupAPI charges it server-side via
/// Spreedly (tokenize + charge in one call) — the raw number/CVV are used only for that
/// call and are never persisted; only masked/partial data and the resulting Spreedly
/// payment_method_token are stored.
/// </summary>
public class CreditCardInfoDto
{
    public string CardHolderFirstName { get; set; } = string.Empty;
    public string CardHolderLastName  { get; set; } = string.Empty;

    /// <summary>Full card number (PAN). Never persisted.</summary>
    public string CardNumber { get; set; } = string.Empty;

    /// <summary>CVV/CVC. Never persisted — Spreedly processes and discards it.</summary>
    public string Cvv { get; set; } = string.Empty;

    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
}
