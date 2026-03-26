namespace MLMConquerorGlobalEdition.Billing.DTOs;

public class PayoutResponse
{
    public int EarningsPaid { get; set; }
    public decimal TotalPaid { get; set; }
    public string MemberId { get; set; } = string.Empty;
}
