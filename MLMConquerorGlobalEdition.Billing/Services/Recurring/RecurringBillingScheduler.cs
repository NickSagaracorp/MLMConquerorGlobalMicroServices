using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Services.Recurring;

/// <summary>
/// Pure date-logic implementation of IRecurringBillingScheduler.
/// No database access, no async — fully deterministic and unit-testable.
/// </summary>
public class RecurringBillingScheduler : IRecurringBillingScheduler
{
    public void Advance(
        SubscriptionBillingState state,
        RecurringBillingPlan plan,
        bool succeeded,
        DateTime attemptDate)
    {
        if (succeeded)
        {
            AdvanceOnSuccess(state, plan, attemptDate);
            return;
        }

        // Check if the 90-day (StopAfterUnbilledDays) hard cap has been reached BEFORE advancing
        // retries, so that even a first failure can trigger the stop if the gap is already huge.
        if (plan.StopAfterUnbilledDays.HasValue)
        {
            var referenceDate = state.LastSuccessfulBillingDate ?? state.BillingAnchorDate;
            var unbilledDays = (attemptDate.Date - referenceDate.Date).TotalDays;
            if (unbilledDays >= plan.StopAfterUnbilledDays.Value)
            {
                state.Status = RecurringBillingStatus.Stopped;
                state.LastAttemptAt = attemptDate;
                state.LastAttemptOutcome = "Failed";
                return;
            }
        }

        var cadence = plan.ParseCadence();
        // CurrentAttemptIndex tracks how many retries have been issued in this cycle.
        // On the very first attempt of a cycle, index == 0; after one retry it becomes 1, etc.
        // cadence[0] = gap after the 1st failure (i.e. the first *retry* attempt).
        // cadence[N-1] = gap after the (N)th failure (last retry).
        // If we've already exhausted all cadence entries → cycle is done.

        if (state.CurrentAttemptIndex < cadence.Length)
        {
            // Retries remain
            var offsetDays = cadence[state.CurrentAttemptIndex];
            state.NextAttemptDate = attemptDate.Date.AddDays(offsetDays);
            state.CurrentAttemptIndex++;
            state.Status = RecurringBillingStatus.Retrying;
            state.LastAttemptAt = attemptDate;
            state.LastAttemptOutcome = "Failed";
        }
        else
        {
            // Cadence exhausted — apply the OnAllRetriesFail policy
            AdvanceOnCadenceExhausted(state, plan, attemptDate);
        }
    }

    // ── Success path ─────────────────────────────────────────────────────────

    private static void AdvanceOnSuccess(
        SubscriptionBillingState state,
        RecurringBillingPlan plan,
        DateTime attemptDate)
    {
        var nextBillingDate = plan.CycleType == RecurringCycleType.Every30Days
            ? attemptDate.AddDays(30)
            : attemptDate.AddYears(1);

        state.LastSuccessfulBillingDate = attemptDate;
        state.NextBillingDate           = nextBillingDate;
        state.NextAttemptDate           = nextBillingDate;
        state.CurrentAttemptIndex       = 0;
        state.Status                    = RecurringBillingStatus.Active;
        state.LastAttemptAt             = attemptDate;
        state.LastAttemptOutcome        = "Success";
        state.LastFailureReason         = null;
    }

    // ── Cadence exhausted path ────────────────────────────────────────────────

    private static void AdvanceOnCadenceExhausted(
        SubscriptionBillingState state,
        RecurringBillingPlan plan,
        DateTime attemptDate)
    {
        state.LastAttemptAt      = attemptDate;
        state.LastAttemptOutcome = "Failed";

        if (plan.OnAllRetriesFail == RecurringFailurePolicy.MarkExpired)
        {
            state.Status = RecurringBillingStatus.Stopped;
            return;
        }

        // RetryOnMonthlyAnniversary: schedule on BillingAnchorDate day-of-month in the following month.
        // First check whether the StopAfterUnbilledDays cap would be triggered by that anniversary.
        var anniversaryDate = NextMonthlyAnniversary(state.BillingAnchorDate, attemptDate);

        if (plan.StopAfterUnbilledDays.HasValue)
        {
            var referenceDate = state.LastSuccessfulBillingDate ?? state.BillingAnchorDate;
            var unbilledOnAnniversary = (anniversaryDate.Date - referenceDate.Date).TotalDays;
            if (unbilledOnAnniversary >= plan.StopAfterUnbilledDays.Value)
            {
                state.Status = RecurringBillingStatus.Stopped;
                return;
            }
        }

        state.NextBillingDate     = anniversaryDate;
        state.NextAttemptDate     = anniversaryDate;
        state.CurrentAttemptIndex = 0;
        state.Status              = RecurringBillingStatus.AwaitingAnniversaryRetry;
    }

    // ── Date helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the date whose month is one after <paramref name="referenceMonth"/> (month of the
    /// latest failed attempt) and whose day is the day-of-month of <paramref name="anchorDate"/>,
    /// clamped to the last day of that month when the anchor day exceeds the month length.
    /// </summary>
    public static DateTime NextMonthlyAnniversary(DateTime anchorDate, DateTime referenceMonth)
    {
        // Target month = the month following the cycle that just exhausted
        var targetYear  = referenceMonth.Year;
        var targetMonth = referenceMonth.Month + 1;
        if (targetMonth > 12)
        {
            targetMonth = 1;
            targetYear++;
        }

        var daysInTargetMonth = DateTime.DaysInMonth(targetYear, targetMonth);
        var targetDay = Math.Min(anchorDate.Day, daysInTargetMonth);
        return new DateTime(targetYear, targetMonth, targetDay, 0, 0, 0, DateTimeKind.Utc);
    }
}
