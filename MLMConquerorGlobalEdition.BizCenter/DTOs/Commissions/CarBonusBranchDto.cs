namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;

public class CarBonusBranchDto
{
    public List<CarBonusBranchMemberDto> Members { get; set; } = new();
}

public class CarBonusBranchMemberDto
{
    public string    OrderNo         { get; set; } = string.Empty;
    public string    FullName        { get; set; } = string.Empty;
    public string    MembershipLevel { get; set; } = string.Empty;
    public DateTime? ExpirationDate  { get; set; }
    public int       Points          { get; set; }
}
