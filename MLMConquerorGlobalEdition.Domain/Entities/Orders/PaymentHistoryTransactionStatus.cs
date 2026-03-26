namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

public enum PaymentHistoryTransactionStatus
{
    Pending = 1,
    Authorized = 2,
    Captured = 3,
    Failed = 4,
    Refunded = 5,
    Voided = 6
}
