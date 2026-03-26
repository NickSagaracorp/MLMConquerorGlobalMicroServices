using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

public class PaymentHistory : AuditChangesStringKey
{
    public string OrderId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string GatewayName { get; set; } = string.Empty;
    public string? GatewayTransactionId { get; set; }
    public PaymentHistoryTransactionStatus TransactionStatus { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
