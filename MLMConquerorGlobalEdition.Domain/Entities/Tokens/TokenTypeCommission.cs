using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tokens;

public class TokenTypeCommission : AuditChangesIntKey
{
    public int TokenTypeId { get; set; }
    public int CommissionTypeId { get; set; }
    public decimal CommissionPerToken { get; set; }

    // Commission triggers — controls which commissions fire for this token type
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
