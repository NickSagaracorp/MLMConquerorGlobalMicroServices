namespace MLMConquerorGlobalEdition.Repository.Services.Ranks;

/// <summary>
/// Single-source-of-truth shape for rank-related data shown in BizCenter and Admin
/// (residuals page, dashboard widgets, member profile, etc.).
/// </summary>
public class RankSummaryDto
{
    public string MemberId { get; set; } = string.Empty;

    /// <summary>The rank the member CURRENTLY qualifies for, computed live from
    /// their actual DualTeamPoints + EnrollmentPoints (not from history).</summary>
    public string? CurrentRankName              { get; set; }
    public int?    CurrentRankId                { get; set; }
    public int     CurrentRankSortOrder         { get; set; }
    public int     CurrentRankDualTeamPoints    { get; set; }
    public int     CurrentRankEnrollmentPoints  { get; set; }
    /// <summary>Member's CAPPED dual-team points eligible toward the current
    /// rank threshold (per-leg cap applied, then capped at the rank's
    /// TeamPoints requirement so the value never exceeds the threshold).
    /// Threshold = 0 means this dimension does not apply at this rank.</summary>
    public int     CurrentRankEligibleDualTeamPoints   { get; set; }
    public int     CurrentRankEligibleEnrollmentPoints { get; set; }

    /// <summary>The next rank above the current. Null when already at the top.</summary>
    public string? NextRankName                 { get; set; }
    public int?    NextRankId                   { get; set; }
    public int     NextRankSortOrder            { get; set; }
    public int     NextRankDualTeamPoints       { get; set; }
    public int     NextRankEnrollmentPoints     { get; set; }
    public int     NextRankEligibleDualTeamPoints   { get; set; }
    public int     NextRankEligibleEnrollmentPoints { get; set; }

    /// <summary>Highest rank ever achieved (preserved from history; survives regressions).</summary>
    public string? LifetimeRankName { get; set; }
    public int?    LifetimeRankId   { get; set; }

    public int DualTeamPoints            { get; set; }
    public int EnrollmentPoints          { get; set; }
    public int QualifiedSponsoredMembers { get; set; }
    public int EnrollmentTeamSize        { get; set; }
}
