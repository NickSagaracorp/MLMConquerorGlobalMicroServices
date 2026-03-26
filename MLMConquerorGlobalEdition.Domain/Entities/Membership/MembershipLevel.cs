using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Membership;

public class MembershipLevel : AuditChangesIntKey
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal RenewalPrice { get; set; }
    public int SortOrder { get; set; }
    public bool IsFree { get; set; }
    public bool IsAutoRenew { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<MembershipLevelBenefit> Benefits { get; set; } = new List<MembershipLevelBenefit>();
}
