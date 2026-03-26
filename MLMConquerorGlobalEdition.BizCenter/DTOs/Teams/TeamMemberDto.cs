namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;

public class TeamMemberDto
{
    public string MemberId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string MemberType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime EnrollDate { get; set; }
    public string? SponsorMemberId { get; set; }
}
