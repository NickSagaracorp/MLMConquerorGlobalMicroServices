namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Billing;

public class AddCreditCardRequest
{
    public string Last4 { get; set; } = string.Empty;
    public string First6 { get; set; } = string.Empty;
    public string CardBrand { get; set; } = string.Empty;
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
    public string Gateway { get; set; } = string.Empty;
    public string GatewayToken { get; set; } = string.Empty;
    public string CardToken { get; set; } = string.Empty;
    public string MaskedCardNumber { get; set; } = string.Empty;
}
