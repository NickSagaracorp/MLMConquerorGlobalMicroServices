using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Services.Recurring;

/// <summary>
/// Pure date-logic service — no I/O. Given the current state, plan, and the outcome of the
/// just-completed attempt, computes the next (CurrentAttemptIndex, NextAttemptDate,
/// NextBillingDate, Status) tuple.
/// </summary>
public interface IRecurringBillingScheduler
{
    /// <summary>
    /// Mutates <paramref name="state"/> in place with the correct next scheduling values.
    /// Call inside the same transaction as the charge so the state change is atomic.
    /// </summary>
    /// <param name="state">The billing state row to advance.</param>
    /// <param name="plan">The governing billing plan (includes cadence and policies).</param>
    /// <param name="succeeded">True if the attempt just made was successful.</param>
    /// <param name="attemptDate">The date/time at which the attempt was made (from IDateTimeProvider).</param>
    void Advance(
        SubscriptionBillingState state,
        RecurringBillingPlan plan,
        bool succeeded,
        DateTime attemptDate);
}
