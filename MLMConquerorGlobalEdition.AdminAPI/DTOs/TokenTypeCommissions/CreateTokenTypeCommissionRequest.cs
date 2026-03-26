namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenTypeCommissions;

public class CreateTokenTypeCommissionRequest
{
    public int TokenTypeId { get; set; }
    public int CommissionTypeId { get; set; }
    public decimal CommissionPerToken { get; set; }
    public bool TriggerSponsorBonus { get; set; } = true;
    public bool TriggerBuilderBonus { get; set; } = true;
    public bool TriggerSponsorBonusTurbo { get; set; } = false;
    public bool TriggerBuilderBonusTurbo { get; set; } = false;
    public bool TriggerFastStartBonus { get; set; } = true;
    public bool TriggerBoostBonus { get; set; } = true;
    public bool CarBonusEligible { get; set; } = true;
    public bool PresidentialBonusEligible { get; set; } = true;
    public bool EligibleMembershipResidual { get; set; } = true;
    public bool EligibleDailyResidual { get; set; } = true;
}
