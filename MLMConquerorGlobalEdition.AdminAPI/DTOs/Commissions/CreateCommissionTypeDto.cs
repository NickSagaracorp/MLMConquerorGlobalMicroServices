namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;

public class CreateCommissionTypeDto
{
    public int CommissionCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Percentage { get; set; }
    public int PaymentDelayDays { get; set; }
    public bool IsRealTime { get; set; }
    public bool IsPaidOnSignup { get; set; }
    public bool IsPaidOnRenewal { get; set; }
    public bool Cummulative { get; set; }
    public int TriggerOrder { get; set; }
    public int NewMembers { get; set; }
    public int DaysAfterJoining { get; set; }
    public int MembersRebill { get; set; }
    public int LifeTimeRank { get; set; }
    public int CurrentRank { get; set; }
    public int LevelNo { get; set; }
    public bool ResidualBased { get; set; }
    public int ResidualOverCommissionType { get; set; }
    public double ResidualPercentage { get; set; }
    public int PersonalPoints { get; set; }
    public int TeamPoints { get; set; }
    public double MaxTeamPointsPerBranch { get; set; } = 0.5;
    public int EnrollmentTeam { get; set; }
    public double MaxEnrollmentTeamPointsPerBranch { get; set; } = 0.5;
    public int ExternalMembers { get; set; }
    public int SponsoredMembers { get; set; }
    public int ReverseId { get; set; }
}
