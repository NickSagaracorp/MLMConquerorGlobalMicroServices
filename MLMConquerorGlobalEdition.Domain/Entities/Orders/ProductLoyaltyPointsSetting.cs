using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

public class ProductLoyaltyPointsSetting : AuditChangesIntKey
{
    public string ProductId { get; set; } = string.Empty;
    public decimal PointsPerUnit { get; set; }
    public int RequiredSuccessfulPayments { get; set; }
    public bool IsActive { get; set; } = true;
}
