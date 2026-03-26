namespace MLMConquerorGlobalEdition.Billing.DTOs;

public class RefundRequest
{
    public string PaymentHistoryId { get; set; } = string.Empty;

    /// <summary>Null means full refund of the original payment amount.</summary>
    public decimal? Amount { get; set; }

    public string? Reason { get; set; }
}
