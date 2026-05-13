using MLMConquerorGlobalEdition.Domain.Entities.Membership;

namespace MLMConquerorGlobalEdition.Billing.Services.Recurring;

/// <summary>
/// Ensures a SubscriptionBillingState exists and is correctly initialised for a subscription
/// that belongs to a product governed by a RecurringBillingPlan.
/// </summary>
public interface IRecurringBillingEnrollmentService
{
    /// <summary>
    /// Called when a new subscription is created (or renewed via the normal signup/order flow).
    /// Looks up the active RecurringBillingPlan for the subscription's product and creates
    /// (or refreshes) the SubscriptionBillingState:
    ///   - BillingAnchorDate = subscription.StartDate
    ///   - NextBillingDate   = StartDate + cycle (30d or 1y)
    ///   - CurrentAttemptIndex = 0
    ///   - Status = Active
    /// If no plan governs this product, returns without error (non-recurring product).
    /// </summary>
    Task EnsureStateForSubscriptionAsync(
        MembershipSubscription subscription,
        string actor,
        CancellationToken ct = default);
}
