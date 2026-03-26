using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

public class Orders : AuditChangesStringKey
{
    public string MemberId { get; set; } = string.Empty;
    public string? MembershipSubscriptionId { get; set; }   // links order ↔ subscription
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime OrderDate { get; set; }
    public string? Notes { get; set; }
    public string? CheckoutScreenshotUrl { get; set; }       // S3 URL — chargeback evidence

    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    public ICollection<PaymentHistory> Payments { get; set; } = new List<PaymentHistory>();
}
