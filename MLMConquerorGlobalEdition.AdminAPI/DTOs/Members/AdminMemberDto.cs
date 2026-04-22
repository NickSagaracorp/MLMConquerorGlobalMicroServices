namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;

public class AdminMemberDto
{
    public string MemberId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string Country { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string MemberType { get; set; } = string.Empty;
    public DateTime EnrollDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? SponsorMemberId { get; set; }
    public string? SponsorFullName { get; set; }
    public string? DualTeamParentMemberId { get; set; }
    public string? DualTeamUplineFullName { get; set; }
    public string? ReplicateSiteSlug { get; set; }
    public string? MembershipLevelName { get; set; }
    public string? LifetimeRankName { get; set; }
    public DateTime CreationDate { get; set; }
}
