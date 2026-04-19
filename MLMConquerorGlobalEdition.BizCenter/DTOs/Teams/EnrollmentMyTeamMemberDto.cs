namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;

public class EnrollmentMyTeamMemberDto
{
    public string   MemberId            { get; set; } = string.Empty;
    public string   FullName            { get; set; } = string.Empty;
    public string   Email               { get; set; } = string.Empty;
    public string?  Phone               { get; set; }
    public string   Country             { get; set; } = string.Empty;
    public int      Level               { get; set; }
    public DateTime EnrollDate          { get; set; }
    public string?  SponsorMemberId     { get; set; }
    public string?  SponsorFullName     { get; set; }
    public string?  DualUplineMemberId  { get; set; }
    public string?  DualUplineFullName  { get; set; }

    // Status columns
    public string   AccountStatus       { get; set; } = string.Empty;   // Active / Inactive / Suspended
    public string   MembershipStatus    { get; set; } = string.Empty;   // Active / Suspended / Cancelled
    public bool     IsQualified         { get; set; }

    // Membership
    public string?  MembershipLevelName { get; set; }

    // Rank
    public string?  CurrentRankName     { get; set; }
    public DateTime? RankDate           { get; set; }
    public string?  LifetimeRankName    { get; set; }
    public int      NextRankPercent     { get; set; }   // 0-100

    // Points
    public int      QualificationPoints    { get; set; }
    public int      EnrollmentTeamPoints   { get; set; }
    public decimal  LeftTeamPoints         { get; set; }
    public decimal  RightTeamPoints        { get; set; }

    // Conditional dates
    public DateTime? SuspensionDate   { get; set; }
    public DateTime? CancellationDate { get; set; }
    public DateTime? LastPaymentDate  { get; set; }
}
