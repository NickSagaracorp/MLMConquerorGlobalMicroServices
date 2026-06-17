namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// Single source of truth for dual-team "My Team" payloads. Mirrors
/// <see cref="EnrollmentMyTeamMemberView"/> field-for-field plus a Leg column
/// (Left / Right) that names which side of the viewer's binary tree each
/// downline sits on. Used by both BizCenter (member's own view) and Admin
/// (member profile drill-down). Do NOT duplicate this shape elsewhere.
/// </summary>
/// <summary>Left/right leg totals for a member's binary position, plus the per-leg points cap
/// for the member's NEXT rank (MaxTeamPointsPerBranch × nextRank.TeamPoints; 0 = the dual-team
/// dimension does not apply at that rank). Single shape consumed by BOTH the Admin
/// dual-tree/stats endpoint and the BizCenter one — do not recompute leg points anywhere else.</summary>
public class DualTreeStatsView
{
    public decimal LeftLegPoints  { get; set; }
    public decimal RightLegPoints { get; set; }
    public int     NextRankLegCap { get; set; }
    public string? NextRankName   { get; set; }
}

/// <summary>One ancestor on the path from the binary-tree root down to a node — used by the
/// visualizer to open (drill to) the branch that contains a searched/deepest node.</summary>
public class DualTreePathNodeView
{
    public string MemberId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

/// <summary>A binary-subtree search hit: the matched node plus its path from the root (gateway →
/// … → match), the leg it sits on, and its depth below the root. The UI navigates the tree to an
/// ancestor on <see cref="Path"/> so the match becomes visible, then highlights it.</summary>
public class DualTreeSearchMatchView
{
    public string                    MemberId { get; set; } = string.Empty;
    public string                    FullName { get; set; } = string.Empty;
    public string                    Leg      { get; set; } = string.Empty; // "Left" / "Right" / ""
    public int                       Depth    { get; set; }                 // levels below the root
    public List<DualTreePathNodeView> Path    { get; set; } = new();        // root-exclusive → match (last = match)
}

/// <summary>Navigation target for the "go to deepest of left/right leg" arrows: the deepest node
/// on a leg plus its path so the UI can drill straight to it.</summary>
public class DualTreeNavTargetView
{
    public string                    MemberId { get; set; } = string.Empty;
    public string                    FullName { get; set; } = string.Empty;
    public int                       Depth    { get; set; }
    public List<DualTreePathNodeView> Path    { get; set; } = new();
}

/// <summary>One month bucket on the Total Dual Team Points trend chart.
/// Keeps both legs separate so the UI can render grouped Left/Right bars and
/// derive Total client-side without a second round-trip.</summary>
public class DualLegMonthlyPointView
{
    public int     Year           { get; set; }
    public int     Month          { get; set; }   // 1..12
    public decimal LeftLegPoints  { get; set; }
    public decimal RightLegPoints { get; set; }
}

/// <summary>
/// Single row of the three-row "Dual Team Members" feed shown on Residuals:
/// the viewer (Leg = "Root") followed by their left and right gateway
/// children. The donut percent denominator differs per leg: root divides by
/// the rank's TeamPoints threshold; children divide by the per-leg cap so
/// each child donut tops out at the leg's individual contribution limit.
/// </summary>
public class DualLegRowView
{
    public string  MemberId                  { get; set; } = string.Empty;
    public string  FullName                  { get; set; } = string.Empty;
    public string  Leg                       { get; set; } = string.Empty; // "Root" | "Left" | "Right"
    public string? RankName                  { get; set; }                 // only set for Root
    public int     QualificationPoints       { get; set; }
    public int     CurrentRankEligiblePoints { get; set; }
    public int     CurrentRankEligiblePct    { get; set; }
    public int     NextRankEligiblePoints    { get; set; }
    public int     NextRankEligiblePct       { get; set; }
}

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
