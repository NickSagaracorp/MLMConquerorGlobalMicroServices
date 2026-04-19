namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;

public class EnrollmentCustomerDto
{
    public string    MemberId         { get; set; } = string.Empty;
    public string    FullName         { get; set; } = string.Empty;
    public string    Email            { get; set; } = string.Empty;
    public string?   Phone            { get; set; }
    public string    Country          { get; set; } = string.Empty;
    public int       Level            { get; set; }
    public DateTime  EnrollDate       { get; set; }
    public string?   SponsorMemberId  { get; set; }
    public string?   SponsorFullName  { get; set; }
    public string    AccountStatus    { get; set; } = string.Empty;
    public string    MembershipStatus { get; set; } = string.Empty;
    public string?   MembershipLevel  { get; set; }
    public int       PersonalPoints   { get; set; }
}
