namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.ProductLoyaltyPoints;

public class CreateProductLoyaltyPointsRequest
{
    public decimal PointsPerUnit { get; set; }
    public int RequiredSuccessfulPayments { get; set; }
}
