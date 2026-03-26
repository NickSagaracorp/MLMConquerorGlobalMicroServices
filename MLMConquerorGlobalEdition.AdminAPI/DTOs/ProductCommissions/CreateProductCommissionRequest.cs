namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.ProductCommissions;

public class CreateProductCommissionRequest
{
    public string ProductId { get; set; } = string.Empty;
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
