namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Billing;

/// <summary>
/// Raw credit-card payload submitted from the BizCenter "Payment Method" tab.
/// Server-side handler tokenizes it (via Spreedly/Stripe — currently simulated),
/// encrypts the leading 12 digits, and persists only the masked last 4 for display.
/// The raw <see cref="CardNumber"/> never reaches the database.
/// </summary>
public class AddCreditCardRequest
{
    public string CardNumber       { get; set; } = string.Empty;
    public string CardholderName   { get; set; } = string.Empty;
    public int    ExpiryMonth      { get; set; }
    public int    ExpiryYear       { get; set; }
    public string Cvv              { get; set; } = string.Empty;
}

public class ReorderCreditCardsRequest
{
    public List<string> OrderedCardIds { get; set; } = new();
}
