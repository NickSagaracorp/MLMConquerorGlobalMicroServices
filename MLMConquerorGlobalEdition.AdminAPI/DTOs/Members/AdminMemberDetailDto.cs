namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;

public class AdminMemberDetailDto
{
    public string MemberId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Country { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string MemberType { get; set; } = string.Empty;
    public DateTime EnrollDate { get; set; }
    public string? SponsorMemberId { get; set; }
    public DateTime CreationDate { get; set; }
    public int DualTeamPoints { get; set; }
    public int EnrollmentPoints { get; set; }
    public int DualTeamSize { get; set; }
    public int EnrollmentTeamSize { get; set; }
    public decimal CurrentMonthIncome { get; set; }
    public decimal CurrentYearIncome { get; set; }
}
