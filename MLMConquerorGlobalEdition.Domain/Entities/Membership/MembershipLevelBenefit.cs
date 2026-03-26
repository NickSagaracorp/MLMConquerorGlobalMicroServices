using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Membership;

public class MembershipLevelBenefit : AuditChangesIntKey
{
    public int MembershipLevelId { get; set; }
    public string BenefitName { get; set; } = string.Empty;
    public string? BenefitDescription { get; set; }
    public string? BenefitValue { get; set; }
}
