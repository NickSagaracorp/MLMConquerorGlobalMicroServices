namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;

public class UpsertPromoProductCommissionRequest
{
    public string ProductId { get; set; } = string.Empty;
    public bool TriggerSponsorBonus { get; set; } = true;
    public bool TriggerBuilderBonus { get; set; }
    public bool TriggerFastStartBonus { get; set; }
    public bool TriggerBoostBonus { get; set; }
    public bool CarBonusEligible { get; set; } = true;
    public bool PresidentialBonusEligible { get; set; } = true;
    public bool EligibleMembershipResidual { get; set; } = true;
    public bool EligibleDailyResidual { get; set; }
}
