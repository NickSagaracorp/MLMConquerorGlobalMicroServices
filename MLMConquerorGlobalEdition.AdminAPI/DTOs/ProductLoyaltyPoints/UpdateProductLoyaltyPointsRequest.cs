namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.ProductLoyaltyPoints;

public class UpdateProductLoyaltyPointsRequest
{
    public decimal PointsPerUnit { get; set; }
    public int RequiredSuccessfulPayments { get; set; }
    public bool IsActive { get; set; }
}
