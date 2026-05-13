namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;

/// <summary>
/// Row shape for the BizCenter Dual Team Members table on the Residuals page.
/// Mirrors AdminAPI's <c>DualTeamMemberDto</c> in the fields the UI consumes —
/// FullName / Leg / QualificationPoints plus the current/next rank eligibility
/// (capped at the rank's per-leg DT cap) the donut renders. cap = 0 means the
/// DT dimension does not apply at the viewer's rank and the client collapses
/// the donut to "—".
/// </summary>
public class TeamMemberDto
{
    public string   MemberId                  { get; set; } = string.Empty;
    public string   FullName                  { get; set; } = string.Empty;
    public string   FirstName                 { get; set; } = string.Empty;
    public string   LastName                  { get; set; } = string.Empty;
    public string   MemberType                { get; set; } = string.Empty;
    public string   Status                    { get; set; } = string.Empty;
    public DateTime EnrollDate                { get; set; }
    public string?  SponsorMemberId           { get; set; }
    public string   Leg                       { get; set; } = string.Empty;
    public int      QualificationPoints       { get; set; }
    public int      CurrentRankEligiblePoints { get; set; }
    public int      CurrentRankEligiblePct    { get; set; }
    public int      NextRankEligiblePoints    { get; set; }
    public int      NextRankEligiblePct       { get; set; }
}
