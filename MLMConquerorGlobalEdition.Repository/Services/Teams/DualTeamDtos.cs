namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// Single source of truth for dual-team "My Team" payloads. Mirrors
/// <see cref="EnrollmentMyTeamMemberView"/> field-for-field plus a Leg column
/// (Left / Right) that names which side of the viewer's binary tree each
/// downline sits on. Used by both BizCenter (member's own view) and Admin
/// (member profile drill-down). Do NOT duplicate this shape elsewhere.
/// </summary>
public class DualTeamMyTeamMemberView
{
    public string    MemberId            { get; set; } = string.Empty;
    public string    FullName            { get; set; } = string.Empty;
    public string    Email               { get; set; } = string.Empty;
    public string?   Phone               { get; set; }
    public string    Country             { get; set; } = string.Empty;
    public int       Level               { get; set; }
    public string    Leg                 { get; set; } = string.Empty;   // Left | Right
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
}
