namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenTypeCommissions;

public class TokenTypeCommissionDto
{
    public int Id { get; set; }
    public int TokenTypeId { get; set; }
    public int CommissionTypeId { get; set; }
    public decimal CommissionPerToken { get; set; }
    public bool TriggerSponsorBonus { get; set; }
    public bool TriggerBuilderBonus { get; set; }
    public bool TriggerSponsorBonusTurbo { get; set; }
    public bool TriggerBuilderBonusTurbo { get; set; }
    public bool TriggerFastStartBonus { get; set; }
    public bool TriggerBoostBonus { get; set; }
    public bool CarBonusEligible { get; set; }
    public bool PresidentialBonusEligible { get; set; }
    public bool EligibleMembershipResidual { get; set; }
    public bool EligibleDailyResidual { get; set; }
}
