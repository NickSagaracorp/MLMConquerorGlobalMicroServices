namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;

public class CorporatePromoDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? BannerUrl { get; set; }
    public bool IsActive { get; set; }
    public int MemberCount { get; set; }
    public DateTime CreationDate { get; set; }
}
