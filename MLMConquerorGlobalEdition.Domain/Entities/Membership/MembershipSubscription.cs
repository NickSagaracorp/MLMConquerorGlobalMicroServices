using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;

namespace MLMConquerorGlobalEdition.Domain.Entities.Membership;

public class MembershipSubscription : AuditChangesStringKey
{
    public string MemberId { get; set; } = string.Empty;
    public int MembershipLevelId { get; set; }
    public int? PreviousMembershipLevelId { get; set; }
    public SubscriptionChangeReason ChangeReason { get; set; }
    public MembershipStatus SubscriptionStatus { get; set; } = MembershipStatus.Active;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? RenewalDate { get; set; }
    public DateTime? HoldDate { get; set; }
    public DateTime? CancellationDate { get; set; }
    public bool IsFree { get; set; }
    public bool IsAutoRenew { get; set; }

    /// <summary>
    /// The most recent order linked to this subscription.
    /// Set on signup and updated on every renewal.
    /// </summary>
    public string? LastOrderId { get; set; }

    public MembershipLevel? MembershipLevel { get; set; }
    public Domain.Entities.Orders.Orders? LastOrder { get; set; }

    public void ValidateChange(int newLevelSortOrder, int currentLevelSortOrder, SubscriptionChangeReason reason)
    {
        if (newLevelSortOrder == currentLevelSortOrder)
            throw new MembershipChangeNotAllowedException("Cannot change to the same membership level.");

        if (reason == SubscriptionChangeReason.Upgrade && newLevelSortOrder <= currentLevelSortOrder)
            throw new MembershipChangeNotAllowedException("Upgrade requires a higher sort order level.");

        if (reason == SubscriptionChangeReason.Downgrade && newLevelSortOrder >= currentLevelSortOrder)
            throw new MembershipChangeNotAllowedException("Downgrade requires a lower sort order level.");
    }
}
