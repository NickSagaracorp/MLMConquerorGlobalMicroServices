namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Billing;

public class OrderHistoryDto
{
    public string OrderId { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
