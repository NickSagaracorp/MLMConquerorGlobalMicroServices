namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Ranks;

public class RankDashboardDto
{
    public string MemberId { get; set; } = string.Empty;

    // Current rank
    public string? CurrentRankName              { get; set; }
    public int     CurrentRankSortOrder         { get; set; }
    public int     CurrentRankDualTeamPoints    { get; set; }
    public int     CurrentRankEnrollmentPoints  { get; set; }
    /// <summary>Eligible (capped) DT/ET points toward this rank — for the
    /// "X / threshold" UI on rank cards and the progress-bar denominator on
    /// the totals cards. 0 when that dimension does not apply at this rank.</summary>
    public int     CurrentRankEligibleDualTeamPoints   { get; set; }
    public int     CurrentRankEligibleEnrollmentPoints { get; set; }

    // Next rank (null/0 when already at top rank)
    public string? NextRankName                 { get; set; }
    public int     NextRankSortOrder            { get; set; }
    public int     NextRankDualTeamPoints       { get; set; }
    public int     NextRankEnrollmentPoints     { get; set; }
    public int     NextRankEligibleDualTeamPoints   { get; set; }
    public int     NextRankEligibleEnrollmentPoints { get; set; }

    public string? LifetimeRankName { get; set; }

    // Member's accumulated points
    public int DualTeamPoints            { get; set; }
    public int EnrollmentPoints          { get; set; }
    public int QualifiedSponsoredMembers { get; set; }
    public int EnrollmentTeamSize        { get; set; }
}
