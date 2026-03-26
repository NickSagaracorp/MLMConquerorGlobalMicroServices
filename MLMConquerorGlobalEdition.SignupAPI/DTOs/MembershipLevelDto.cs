namespace MLMConquerorGlobalEdition.SignupAPI.DTOs;

public class MembershipLevelDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal RenewalPrice { get; set; }
    public int SortOrder { get; set; }
    public bool IsFree { get; set; }
    public bool IsAutoRenew { get; set; }
}
