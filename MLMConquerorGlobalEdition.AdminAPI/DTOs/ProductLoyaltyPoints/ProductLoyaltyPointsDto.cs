namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.ProductLoyaltyPoints;

public class ProductLoyaltyPointsDto
{
    public int Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public decimal PointsPerUnit { get; set; }
    public int RequiredSuccessfulPayments { get; set; }
    public bool IsActive { get; set; }
}
