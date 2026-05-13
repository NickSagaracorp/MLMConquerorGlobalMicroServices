using FluentAssertions;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

public class RecurringBillingSchedulerTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static readonly DateTime Anchor = new(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

    private static SubscriptionBillingState BuildState(
        DateTime? anchor = null,
        DateTime? lastSuccess = null,
        int attemptIndex = 0,
        RecurringBillingStatus status = RecurringBillingStatus.Active)
    {
        var a = anchor ?? Anchor;
        return new SubscriptionBillingState
        {
            Id                       = Guid.NewGuid().ToString(),
            MemberId                 = "mem-1",
            MembershipSubscriptionId = Guid.NewGuid().ToString(),
            RecurringBillingPlanId   = 1,
            BillingAnchorDate        = a,
            NextBillingDate          = a.AddDays(30),
            NextAttemptDate          = a.AddDays(30),
            CurrentAttemptIndex      = attemptIndex,
            Status                   = status,
            LastSuccessfulBillingDate = lastSuccess,
            CreatedBy                = "test",
            CreationDate             = a,
            LastUpdateDate           = a
        };
    }

    private static RecurringBillingPlan BuildMonthlyPlan(
        string cadence = "1,2,2,2,2,2",
        RecurringFailurePolicy policy = RecurringFailurePolicy.RetryOnMonthlyAnniversary,
        int? stopAfterDays = 90)
    {
        return new RecurringBillingPlan
        {
            Id                          = 1,
            Name                        = "Travel Advantage",
            CycleType                   = RecurringCycleType.Every30Days,
            RetryCadenceDays            = cadence,
            OnAllRetriesFail            = policy,
            StopAfterUnbilledDays       = stopAfterDays,
            PayFromCommissionBalanceFirst = true,
            IsActive                    = true,
            CreatedBy                   = "test",
            CreationDate                = Anchor
        };
    }

    private static RecurringBillingPlan BuildAnnualPlan(
        string cadence = "1,1,1,2,2,5,5",
        RecurringFailurePolicy policy = RecurringFailurePolicy.MarkExpired)
    {
        return new RecurringBillingPlan
        {
            Id                          = 2,
            Name                        = "Lifestyle Ambassador",
            CycleType                   = RecurringCycleType.AnnualFromLastBilling,
            RetryCadenceDays            = cadence,
            OnAllRetriesFail            = policy,
            StopAfterUnbilledDays       = null,
            PayFromCommissionBalanceFirst = false,
            IsActive                    = true,
            CreatedBy                   = "test",
            CreationDate                = Anchor
        };
    }

    // ── Success path ───────────────────────────────────────────────────────────

    [Fact]
    public void Advance_WhenSucceeded_Monthly_SetsNextDatePlus30Days()
    {
        var scheduler = new RecurringBillingScheduler();
        var state     = BuildState();
        var plan      = BuildMonthlyPlan();
        var attempt   = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc);

        scheduler.Advance(state, plan, succeeded: true, attemptDate: attempt);

        state.Status.Should().Be(RecurringBillingStatus.Active);
        state.NextBillingDate.Should().Be(attempt.AddDays(30));
        state.NextAttemptDate.Should().Be(attempt.AddDays(30));
        state.CurrentAttemptIndex.Should().Be(0);
        state.LastSuccessfulBillingDate.Should().Be(attempt);
        state.LastAttemptOutcome.Should().Be("Success");
        state.LastFailureReason.Should().BeNull();
    }

    [Fact]
    public void Advance_WhenSucceeded_Annual_SetsNextDatePlusOneYear()
    {
        var scheduler = new RecurringBillingScheduler();
        var state     = BuildState();
        var plan      = BuildAnnualPlan();
        var attempt   = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc);

        scheduler.Advance(state, plan, succeeded: true, attemptDate: attempt);

        state.Status.Should().Be(RecurringBillingStatus.Active);
        state.NextBillingDate.Should().Be(attempt.AddYears(1));
        state.CurrentAttemptIndex.Should().Be(0);
    }

    // ── Failure / retry path ───────────────────────────────────────────────────

    [Fact]
    public void Advance_FirstFailure_SetsRetryingAndNextAttemptFromCadenceIndex0()
    {
        var scheduler = new RecurringBillingScheduler();
        var state     = BuildState(attemptIndex: 0);
        var plan      = BuildMonthlyPlan(cadence: "1,2,2,2,2,2");
        var attempt   = new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc);

        scheduler.Advance(state, plan, succeeded: false, attemptDate: attempt);

        state.Status.Should().Be(RecurringBillingStatus.Retrying);
        state.NextAttemptDate.Should().Be(attempt.Date.AddDays(1)); // cadence[0] = 1
        state.CurrentAttemptIndex.Should().Be(1);
        state.LastAttemptOutcome.Should().Be("Failed");
    }

    [Fact]
    public void Advance_SecondFailure_UsesNextCadenceEntry()
    {
        var scheduler = new RecurringBillingScheduler();
        var state     = BuildState(attemptIndex: 1); // already had 1 retry
        var plan      = BuildMonthlyPlan(cadence: "1,2,2,2,2,2");
        var attempt   = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);

        scheduler.Advance(state, plan, succeeded: false, attemptDate: attempt);

        state.Status.Should().Be(RecurringBillingStatus.Retrying);
        state.NextAttemptDate.Should().Be(attempt.Date.AddDays(2)); // cadence[1] = 2
        state.CurrentAttemptIndex.Should().Be(2);
    }

    [Fact]
    public void Advance_AllRetriesExhausted_RetryOnMonthlyAnniversary_SetsAwaitingAnniversary()
    {
        // cadence "1,2,2,2,2,2" has 6 entries; index 6 means all exhausted
        var scheduler = new RecurringBillingScheduler();
        var state     = BuildState(anchor: new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                                   attemptIndex: 6);
        var plan      = BuildMonthlyPlan(cadence: "1,2,2,2,2,2",
                                         policy: RecurringFailurePolicy.RetryOnMonthlyAnniversary,
                                         stopAfterDays: null); // disable 90-day cap for this test
        var attempt   = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc);

        scheduler.Advance(state, plan, succeeded: false, attemptDate: attempt);

        state.Status.Should().Be(RecurringBillingStatus.AwaitingAnniversaryRetry);
        // Next anniversary: March 15 (anchor day=15, following month = March)
        state.NextBillingDate.Should().Be(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc));
        state.CurrentAttemptIndex.Should().Be(0);
    }

    [Fact]
    public void Advance_AllRetriesExhausted_MarkExpired_StopsState()
    {
        var scheduler = new RecurringBillingScheduler();
        var state     = BuildState(attemptIndex: 7); // annual cadence "1,1,1,2,2,5,5" has 7 entries
        var plan      = BuildAnnualPlan();
        var attempt   = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

        scheduler.Advance(state, plan, succeeded: false, attemptDate: attempt);

        state.Status.Should().Be(RecurringBillingStatus.Stopped);
    }

    [Fact]
    public void Advance_StopAfterUnbilledDays_TriggersStop_BeforeRetry()
    {
        // StopAfterUnbilledDays = 90. Anchor = Jan 15. Attempt = Apr 16 = 91 days after anchor.
        var scheduler = new RecurringBillingScheduler();
        var anchor    = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var state     = BuildState(anchor: anchor, attemptIndex: 0);
        var plan      = BuildMonthlyPlan(stopAfterDays: 90);
        var attempt   = anchor.AddDays(91); // exceeds the cap

        scheduler.Advance(state, plan, succeeded: false, attemptDate: attempt);

        state.Status.Should().Be(RecurringBillingStatus.Stopped);
    }

    [Fact]
    public void Advance_AnniversaryDate_WouldExceed90DayCap_StopsInsteadOfAwaiting()
    {
        // Anchor day = 15. Attempt on Feb 20 exhausts cadence. Anniversary = March 15.
        // But LastSuccess was Nov 1 → 134 days later = exceeds 90. Should stop.
        var scheduler = new RecurringBillingScheduler();
        var anchor    = new DateTime(2025, 11, 15, 0, 0, 0, DateTimeKind.Utc);
        var state     = BuildState(
            anchor: anchor,
            lastSuccess: new DateTime(2025, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            attemptIndex: 6); // cadence exhausted
        var plan      = BuildMonthlyPlan(cadence: "1,2,2,2,2,2",
                                         policy: RecurringFailurePolicy.RetryOnMonthlyAnniversary,
                                         stopAfterDays: 90);
        var attempt   = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc);

        scheduler.Advance(state, plan, succeeded: false, attemptDate: attempt);

        state.Status.Should().Be(RecurringBillingStatus.Stopped);
    }

    // ── NextMonthlyAnniversary helper ──────────────────────────────────────────

    [Fact]
    public void NextMonthlyAnniversary_Standard_ReturnsNextMonthSameDay()
    {
        var anchor    = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var reference = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc);

        var result = RecurringBillingScheduler.NextMonthlyAnniversary(anchor, reference);

        result.Should().Be(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void NextMonthlyAnniversary_MonthEnd_Clamps_WhenTargetMonthIsShorter()
    {
        // Anchor day = 31, reference month = January → target month = February (28 days in 2026)
        var anchor    = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        var reference = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);

        var result = RecurringBillingScheduler.NextMonthlyAnniversary(anchor, reference);

        result.Day.Should().Be(28); // February 2026 = 28 days
        result.Month.Should().Be(2);
    }

    [Fact]
    public void NextMonthlyAnniversary_December_WrapsToJanuaryNextYear()
    {
        var anchor    = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc);
        var reference = new DateTime(2026, 12, 5, 0, 0, 0, DateTimeKind.Utc);

        var result = RecurringBillingScheduler.NextMonthlyAnniversary(anchor, reference);

        result.Should().Be(new DateTime(2027, 1, 10, 0, 0, 0, DateTimeKind.Utc));
    }
}
