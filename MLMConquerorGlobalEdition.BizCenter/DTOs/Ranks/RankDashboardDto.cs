namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Ranks;

public class RankDashboardDto
{
    public string MemberId { get; set; } = string.Empty;

    // Current rank
    public string? CurrentRankName              { get; set; }
    public int     CurrentRankSortOrder         { get; set; }
    public int     CurrentRankDualTeamPoints    { get; set; }
    public int     CurrentRankEnrollmentPoints  { get; set; }

    // Next rank (null/0 when already at top rank)
    public string? NextRankName                 { get; set; }
    public int     NextRankSortOrder            { get; set; }
    public int     NextRankDualTeamPoints       { get; set; }
    public int     NextRankEnrollmentPoints     { get; set; }

    public string? LifetimeRankName { get; set; }

    // Member's accumulated points
    public int DualTeamPoints            { get; set; }
    public int EnrollmentPoints          { get; set; }
    public int QualifiedSponsoredMembers { get; set; }
    public int EnrollmentTeamSize        { get; set; }
}
