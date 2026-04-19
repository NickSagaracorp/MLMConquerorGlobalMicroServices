namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;

public class BranchDetailDto
{
    public string BranchMemberId    { get; set; } = string.Empty;
    public string BranchMemberName  { get; set; } = string.Empty;
    public int    TotalPoints       { get; set; }
    public List<BranchAmbassadorItemDto> Ambassadors { get; set; } = new();
    public List<BranchCustomerItemDto>   Customers   { get; set; } = new();
}

public class BranchAmbassadorItemDto
{
    public int     SeqNo              { get; set; }
    public int     Level              { get; set; }
    public string  FullName           { get; set; } = string.Empty;
    public string  AccountStatus      { get; set; } = string.Empty;
    public string  MembershipStatus   { get; set; } = string.Empty;
    public bool    IsQualified        { get; set; }
    public string? MembershipLevelName{ get; set; }
    public int     EnrollmentPoints   { get; set; }
}

public class BranchCustomerItemDto
{
    public int     SeqNo              { get; set; }
    public int     Level              { get; set; }
    public string  FullName           { get; set; } = string.Empty;
    public string  MembershipStatus   { get; set; } = string.Empty;
    public string? MembershipLevelName{ get; set; }
    public int     EnrollmentPoints   { get; set; }
}
