namespace MLMConquerorGlobalEdition.RankEngine.DTOs;

public class RankRequirementResponse
{
    public int Id { get; set; }
    public int LevelNo { get; set; }

    // Qualification thresholds
    public int PersonalPoints { get; set; }
    public int TeamPoints { get; set; }
    public double MaxTeamPointsPerBranch { get; set; }
    public int EnrollmentTeam { get; set; }
    public int PlacementQualifiedTeamMembers { get; set; }
    public int EnrollmentQualifiedTeamMembers { get; set; }
    public double MaxEnrollmentTeamPointsPerBranch { get; set; }
    public int ExternalMembers { get; set; }
    public int SponsoredMembers { get; set; }
    public decimal SalesVolume { get; set; }

    // Bonuses at this rank
    public decimal RankBonus { get; set; }
    public decimal DailyBonus { get; set; }
    public decimal MonthlyBonus { get; set; }
    public int LifetimeHoldingDuration { get; set; }

    // Display
    public string RankDescription { get; set; } = string.Empty;
    public string CurrentRankDescription { get; set; } = string.Empty;
    public string? AchievementMessage { get; set; }
    public string? CertificateUrl { get; set; }
}
