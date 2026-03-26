namespace MLMConquerorGlobalEdition.Billing.DTOs;

public class ChargeResponse
{
    public string PaymentHistoryId { get; set; } = string.Empty;
    public string GatewayTransactionId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Gateway { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
