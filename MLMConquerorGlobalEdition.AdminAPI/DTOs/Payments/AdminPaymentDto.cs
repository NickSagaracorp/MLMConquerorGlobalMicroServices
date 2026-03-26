namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Payments;

public class AdminPaymentDto
{
    public string Id { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string GatewayName { get; set; } = string.Empty;
    public string? GatewayTransactionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreationDate { get; set; }
}
