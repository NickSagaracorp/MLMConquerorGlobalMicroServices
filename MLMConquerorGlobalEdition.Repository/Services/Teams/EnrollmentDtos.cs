using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// Single source of truth for enrollment-team payloads, shared by BizCenter
/// (member's own view) and Admin (member profile drill-down). Field names match
/// the JSON contract consumed by the SharedComponents Blazor components.
/// </summary>
public class EnrollmentMyTeamMemberView
{
    public string    MemberId            { get; set; } = string.Empty;
    public string    FullName            { get; set; } = string.Empty;
    public string    Email               { get; set; } = string.Empty;
    public string?   Phone               { get; set; }
    public string    Country             { get; set; } = string.Empty;
    public int       Level               { get; set; }
    public DateTime  EnrollDate          { get; set; }
    public string?   SponsorMemberId     { get; set; }
    public string?   SponsorFullName     { get; set; }
    public string?   DualUplineMemberId  { get; set; }
    public string?   DualUplineFullName  { get; set; }
    public string    AccountStatus       { get; set; } = string.Empty;
    public string    MembershipStatus    { get; set; } = string.Empty;
    public bool      IsQualified         { get; set; }
    public string?   MembershipLevelName { get; set; }
    public string?   CurrentRankName     { get; set; }
    public DateTime? RankDate            { get; set; }
    public string?   LifetimeRankName    { get; set; }
    public int       NextRankPercent     { get; set; }
    public int       QualificationPoints  { get; set; }
    public int       EnrollmentTeamPoints { get; set; }
    public decimal   LeftTeamPoints       { get; set; }
    public decimal   RightTeamPoints      { get; set; }
    public DateTime? SuspensionDate       { get; set; }
    public DateTime? CancellationDate     { get; set; }
    public DateTime? LastPaymentDate      { get; set; }
}

public class EnrollmentBranchesView
{
    public int                       TotalPoints              { get; set; }
    public int                       TotalEligibleCurrentRank { get; set; }
    public int                       TotalEligibleNextRank    { get; set; }
    public PagedResult<BranchItemView> Branches { get; set; } = new();
}

public class BranchItemView
{
    public string MemberId            { get; set; } = string.Empty;
    public string FullName            { get; set; } = string.Empty;
    public int    TotalPoints         { get; set; }
    public int    EligibleCurrentRank { get; set; }
    public int    EligibleNextRank    { get; set; }
    public int    EligibleCurrentPct  { get; set; }
    public int    EligibleNextPct     { get; set; }
}

public class BranchDetailView
{
    public string BranchMemberId   { get; set; } = string.Empty;
    public string BranchMemberName { get; set; } = string.Empty;
    public int    TotalPoints      { get; set; }
    public List<BranchAmbassadorRow> Ambassadors { get; set; } = new();
    public List<BranchCustomerRow>   Customers   { get; set; } = new();
}

public class BranchAmbassadorRow
{
    public int     SeqNo               { get; set; }
    public int     Level               { get; set; }
    public string  FullName            { get; set; } = string.Empty;
    public string  AccountStatus       { get; set; } = string.Empty;
    public string  MembershipStatus    { get; set; } = string.Empty;
    public bool    IsQualified         { get; set; }
    public string? MembershipLevelName { get; set; }
    public int     EnrollmentPoints    { get; set; }
}

public class BranchCustomerRow
{
    public int     SeqNo               { get; set; }
    public int     Level               { get; set; }
    public string  FullName            { get; set; } = string.Empty;
    public string  MembershipStatus    { get; set; } = string.Empty;
    public string? MembershipLevelName { get; set; }
    public int     EnrollmentPoints    { get; set; }
}

public class EnrollmentCustomerView
{
    public string   MemberId         { get; set; } = string.Empty;
    public string   FullName         { get; set; } = string.Empty;
    public string   Email            { get; set; } = string.Empty;
    public string?  Phone            { get; set; }
    public string   Country          { get; set; } = string.Empty;
    public DateTime EnrollDate       { get; set; }
    public string?  SponsorMemberId  { get; set; }
    public string?  SponsorFullName  { get; set; }
    public string   AccountStatus    { get; set; } = string.Empty;
    public string   MembershipStatus { get; set; } = string.Empty;
    public string?  MembershipLevel  { get; set; }
    public int      PersonalPoints   { get; set; }
}

public class EnrollmentVisualizerStatsView
{
    public int TotalMembers     { get; set; }
    public int TotalQualified   { get; set; }
    public int TotalUnqualified { get; set; }
    public int TotalCancelled   { get; set; }
}

public class EnrollmentVisualizerChildView
{
    public string MemberId    { get; set; } = string.Empty;
    public string FullName    { get; set; } = string.Empty;
    public string StatusCode  { get; set; } = "Q";
    public int    Points      { get; set; }
    public bool   HasChildren { get; set; }
}
