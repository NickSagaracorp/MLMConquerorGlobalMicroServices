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

    /// <summary>
    /// Point contribution snapshot fields (§10.8 of BILLING-RULES).
    /// Set on first successful billing; refreshed on every successful renewal.
    /// The charge worker emits PointDeltaEvent rows using these stored values,
    /// so the upline aggregator never needs to recompute from product tables.
    ///
    /// Source: derived from the product's MonthlyFee as qualification points
    /// (Product.QualificationPoins). When the billing engine renews a subscription
    /// it reads the product's QualificationPoins and stores them here.
    /// This is the only per-product "contribution" value available today.
    /// If the product has QualificationPoins = 0, the contribution is 0 (free/non-qualifying).
    /// </summary>

    /// <summary>
    /// What this active subscription currently contributes to each upline's DualTeamPoints.
    /// Positive integer; 0 if the product carries no DT qualification.
    /// </summary>
    public int DualTeamContribution { get; set; }

    /// <summary>
    /// What this active subscription currently contributes to each upline's EnrollmentPoints.
    /// Positive integer; 0 if the product carries no ET qualification.
    /// </summary>
    public int EnrollmentContribution { get; set; }

    /// <summary>
    /// What this active subscription contributes to the member's own PersonalPoints.
    /// Positive integer; 0 if the product carries no personal qualification.
    /// </summary>
    public int PersonalContribution { get; set; }

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
