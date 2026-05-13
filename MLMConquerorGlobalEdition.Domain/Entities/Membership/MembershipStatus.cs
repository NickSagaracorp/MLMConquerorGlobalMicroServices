namespace MLMConquerorGlobalEdition.Domain.Entities.Membership;

public enum MembershipStatus
{
    Active = 1,
    Pending = 2,
    OnHold = 3,
    Cancelled = 4,
    Expired = 5,

    /// <summary>
    /// Billing engine stopped after 90 days of failed billing.
    /// Distinct from OnHold (support-initiated pause).
    /// Requires a manual bill-now to reactivate.
    /// </summary>
    HoldByBilling = 6
}
