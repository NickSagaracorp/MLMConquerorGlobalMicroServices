namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;

public class CommissionTypeDto
{
    public int Id { get; set; }
    public int CommissionCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Percentage { get; set; }
    public decimal? Amount { get; set; }
    public decimal? AmountPromo { get; set; }
    public int PaymentDelayDays { get; set; }
    public bool IsActive { get; set; }
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
    public double MaxTeamPointsPerBranch { get; set; }
    public int EnrollmentTeam { get; set; }
    public double MaxEnrollmentTeamPointsPerBranch { get; set; }
    public int ExternalMembers { get; set; }
    public int SponsoredMembers { get; set; }
    public int ReverseId { get; set; }
}
