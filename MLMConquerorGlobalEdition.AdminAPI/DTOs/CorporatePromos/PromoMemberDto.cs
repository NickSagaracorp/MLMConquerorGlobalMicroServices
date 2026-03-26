namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;

public class PromoMemberDto
{
    public string MemberId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string MembershipLevel { get; set; } = string.Empty;
    public DateTime SignupDate { get; set; }
}
