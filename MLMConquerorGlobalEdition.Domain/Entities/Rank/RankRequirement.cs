using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Rank;

public class RankRequirement : AuditChangesIntKey
{
    public int RankDefinitionId { get; set; }
    public int LevelNo { get; set; } = 0;

    // Qualification thresholds
    public int PersonalPoints { get; set; } = 0;
    public int TeamPoints { get; set; } = 0;
    public double MaxTeamPointsPerBranch { get; set; } = 0.5;
    public int EnrollmentTeam { get; set; } = 0;
    public int PlacementQualifiedTeamMembers { get; set; } = 0;
    public int EnrollmentQualifiedTeamMembers { get; set; } = 0;
    public double MaxEnrollmentTeamPointsPerBranch { get; set; } = 0.5;
    public int ExternalMembers { get; set; } = 1;
    public int SponsoredMembers { get; set; } = 1;
    public decimal SalesVolume { get; set; } = 0;

    // Bonuses
    public decimal RankBonus { get; set; } = 0;
    public decimal DailyBonus { get; set; } = 0;
    public decimal MonthlyBonus { get; set; } = 0;
    public int LifetimeHoldingDuration { get; set; } = 0;

    // Display / messaging
    public string RankDescription { get; set; } = string.Empty;
    public string CurrentRankDescription { get; set; } = string.Empty;
    public string? AchievementMessage { get; set; }
    public string? CertificateUrl { get; set; }
}
