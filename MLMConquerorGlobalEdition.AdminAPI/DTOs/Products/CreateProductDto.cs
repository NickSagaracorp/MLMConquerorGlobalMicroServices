namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Products;

public class CreateProductDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal MonthlyFee { get; set; }
    public decimal SetupFee { get; set; }
    public decimal Price90Days { get; set; }
    public decimal Price180Days { get; set; }
    public decimal AnnualPrice { get; set; }
    public string? DescriptionPromo { get; set; }
    public decimal MonthlyFeePromo { get; set; }
    public decimal SetupFeePromo { get; set; }
    public string? ImageUrlPromo { get; set; }
    public int QualificationPoins { get; set; }
    public int QualificationPoinsPromo { get; set; }
    public bool CorporateFee { get; set; }
    public bool JoinPageMembership { get; set; }
    public int? MembershipLevelId { get; set; }
    public int OldSystemProductId { get; set; }
    public string? ThemeClass { get; set; }
}
