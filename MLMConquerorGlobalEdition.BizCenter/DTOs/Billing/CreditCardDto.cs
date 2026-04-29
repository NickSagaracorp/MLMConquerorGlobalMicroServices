namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Billing;

public class CreditCardDto
{
    public string Id               { get; set; } = string.Empty;
    public string Last4            { get; set; } = string.Empty;
    public string First6           { get; set; } = string.Empty;
    public string CardBrand        { get; set; } = string.Empty;
    public int    ExpiryMonth      { get; set; }
    public int    ExpiryYear       { get; set; }
    public bool   IsDefault        { get; set; }
    public bool   IsExpired        { get; set; }
    public int    Priority         { get; set; }
    public string MaskedCardNumber { get; set; } = string.Empty;
}
