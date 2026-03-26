namespace MLMConquerorGlobalEdition.RankEngine.DTOs;

public class RankMetricsResponse
{
    public int PersonalPoints { get; set; }
    public decimal TotalTeamPoints { get; set; }
    public decimal LeftLegPoints { get; set; }
    public decimal RightLegPoints { get; set; }
    public decimal QualifyingTeamPoints { get; set; }
    public int EnrollmentTeamCount { get; set; }
    public int PlacementQualifiedTeamMembers { get; set; }
    public int EnrollmentQualifiedTeamMembers { get; set; }
    public int SponsoredMembers { get; set; }
    public int ExternalMembers { get; set; }
    public decimal SalesVolume { get; set; }
}
