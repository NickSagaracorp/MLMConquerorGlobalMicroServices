namespace MLMConquerorGlobalEdition.SignupAPI.DTOs;

public class ProductDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? ThemeClass { get; set; }
    public decimal Price { get; set; }
    public string? Sku { get; set; }
    public bool CorporateFee { get; set; }
    public bool JoinPageMembership { get; set; }
    public int? MembershipLevelId { get; set; }
}
