namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

public enum OrderStatus
{
    Pending = 1,
    Paid = 2,
    Processing = 3,
    Completed = 4,
    Cancelled = 5,
    Refunded = 6
}
