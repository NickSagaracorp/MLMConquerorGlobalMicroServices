using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Commission;

public class CommissionType : AuditChangesIntKey
{
    public int CommissionCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Percentage { get; set; }

    /// <summary>
    /// Standard commission amount configured by Admin. Used when no promo is active.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Promotional commission amount. When set (non-null, > 0), the engine uses this
    /// instead of Amount for the duration of a promotion. Admin clears it to end the promo.
    /// </summary>
    public decimal? AmountPromo { get; set; }

    /// <summary>
    /// Returns AmountPromo when the signup occurred inside an active CorporatePromo period
    /// that covers this commission category (promoActive=true), otherwise returns Amount.
    /// AmountPromo always has a configured value; the promo gate is determined solely by
    /// whether a CorporatePromo is currently active for the product being purchased.
    /// </summary>
    public decimal? GetEffectiveAmount(bool promoActive)
        => promoActive ? AmountPromo : Amount;

    /// <summary>
    /// Standard amount used by batch/sweep jobs and admin tools that have no per-order
    /// promo context. Never returns AmountPromo — promo resolution is caller responsibility.
    /// </summary>
    public decimal? ActiveAmount => Amount;

    public int PaymentDelayDays { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsRealTime { get; set; }

    // Trigger conditions
    public bool IsPaidOnSignup { get; set; } = false;
    public bool IsPaidOnRenewal { get; set; } = false;
    public bool Cummulative { get; set; } = false;
    public int TriggerOrder { get; set; } = 0;
    public int NewMembers { get; set; } = 0;
    public int DaysAfterJoining { get; set; } = 0;
    public int MembersRebill { get; set; } = 0;

    // Rank requirements
    public int LifeTimeRank { get; set; } = 0;
    public int CurrentRank { get; set; } = 0;
    public int LevelNo { get; set; } = 0;

    // Residual configuration
    public bool ResidualBased { get; set; } = false;
    public int ResidualOverCommissionType { get; set; } = 0;
    public double ResidualPercentage { get; set; } = 0;

    // Tree qualification thresholds
    public int PersonalPoints { get; set; } = 0;
    public int TeamPoints { get; set; } = 0;

    /// <summary>
    /// When true, qualification is measured against MemberStatisticEntity.EnrollmentPoints
    /// (Enrollment Team points) instead of DualTeamPoints.
    /// True for Silver/Gold/Platinum DTR tiers (ET-based ranks per comp plan).
    /// </summary>
    public bool IsEnrollmentBased { get; set; } = false;
    public double MaxTeamPointsPerBranch { get; set; } = 0.5;
    public int EnrollmentTeam { get; set; } = 0;
    public double MaxEnrollmentTeamPointsPerBranch { get; set; } = 0.5;
    public int ExternalMembers { get; set; } = 0;
    public int SponsoredMembers { get; set; } = 0;

    // Sponsor bonus flag — identifies the commission type used for one-time sponsor bonuses on signup
    public bool IsSponsorBonus { get; set; } = false;

    // Self-reference for reverse/override commission type
    // When a commission must be reversed (e.g., cancellation within 14 days),
    // the earning with this CommissionType generates a new earning using the ReverseId type.
    public int ReverseId { get; set; } = 0;

    public CommissionCategory? Category { get; set; }
}
