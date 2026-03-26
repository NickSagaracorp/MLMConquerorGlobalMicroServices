using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

public class ProductCommission : AuditChangesIntKey
{
    public string ProductId { get; set; } = string.Empty;
    public virtual Product Product { get; set; } = null!;

    // Commission triggers
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
