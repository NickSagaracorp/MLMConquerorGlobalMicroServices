namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Ranks;

public class RankDashboardDto
{
    public string MemberId { get; set; } = string.Empty;
    public string? CurrentRankName { get; set; }
    public int CurrentRankSortOrder { get; set; }
    public string? LifetimeRankName { get; set; }
    public int DualTeamPoints { get; set; }
    public int EnrollmentPoints { get; set; }
    public int QualifiedSponsoredMembers { get; set; }
    public int EnrollmentTeamSize { get; set; }
}
