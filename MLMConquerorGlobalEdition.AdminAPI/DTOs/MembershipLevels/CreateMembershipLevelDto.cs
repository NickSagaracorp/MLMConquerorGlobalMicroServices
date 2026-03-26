namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.MembershipLevels;

public class CreateMembershipLevelDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal RenewalPrice { get; set; }
    public int SortOrder { get; set; }
    public bool IsFree { get; set; }
    public bool IsAutoRenew { get; set; }
}
