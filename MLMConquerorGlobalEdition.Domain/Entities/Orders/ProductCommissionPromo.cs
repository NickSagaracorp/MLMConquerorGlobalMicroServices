using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Orders;

public class ProductCommissionPromo : AuditChangesIntKey
{
    public string ProductId { get; set; } = string.Empty;
    public virtual Product Product { get; set; } = null!;

    public long CorporatePromoId { get; set; }
    public virtual CorporatePromo CorporatePromo { get; set; } = null!;

    // Commission triggers (promo defaults — more conservative than standard)
    public bool TriggerSponsorBonus { get; set; } = true;
    public bool TriggerBuilderBonus { get; set; } = false;
    public bool TriggerSponsorBonusTurbo { get; set; } = false;
    public bool TriggerBuilderBonusTurbo { get; set; } = false;
    public bool TriggerFastStartBonus { get; set; } = false;
    public bool TriggerBoostBonus { get; set; } = false;
    public bool CarBonusEligible { get; set; } = true;
    public bool PresidentialBonusEligible { get; set; } = true;
    public bool EligibleMembershipResidual { get; set; } = true;
    public bool EligibleDailyResidual { get; set; } = false;
}
