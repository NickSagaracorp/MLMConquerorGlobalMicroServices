namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks;

public class CreateRankRequirementDto
{
    public int RankDefinitionId { get; set; }
    public int LevelNo { get; set; }
    public int PersonalPoints { get; set; }
    public int TeamPoints { get; set; }
    public double MaxTeamPointsPerBranch { get; set; } = 0.5;
    public int EnrollmentTeam { get; set; }
    public int PlacementQualifiedTeamMembers { get; set; }
    public int EnrollmentQualifiedTeamMembers { get; set; }
    public double MaxEnrollmentTeamPointsPerBranch { get; set; } = 0.5;
    public int ExternalMembers { get; set; } = 1;
    public int SponsoredMembers { get; set; } = 1;
    public decimal SalesVolume { get; set; }
    public decimal RankBonus { get; set; }
    public decimal DailyBonus { get; set; }
    public decimal MonthlyBonus { get; set; }
    public int LifetimeHoldingDuration { get; set; }
    public string RankDescription { get; set; } = string.Empty;
    public string CurrentRankDescription { get; set; } = string.Empty;
    public string? AchievementMessage { get; set; }
    public string? CertificateUrl { get; set; }
}
