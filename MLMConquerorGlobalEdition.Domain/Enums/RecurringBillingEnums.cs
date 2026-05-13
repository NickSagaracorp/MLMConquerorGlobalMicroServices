namespace MLMConquerorGlobalEdition.Domain.Enums;

/// <summary>How often a recurring billing plan cycles.</summary>
public enum RecurringCycleType
{
    /// <summary>Charge every 30 days from the last successful billing date.</summary>
    Every30Days = 1,

    /// <summary>Charge annually (one year from last successful billing date).</summary>
    AnnualFromLastBilling = 2
}

/// <summary>What the engine does when all retry attempts in a cycle are exhausted.</summary>
public enum RecurringFailurePolicy
{
    /// <summary>
    /// Schedule a fresh attempt on the monthly anniversary of the BillingAnchorDate
    /// (the day-of-month of original enrollment) in the following month.
    /// Used by Travel Advantage plans.
    /// </summary>
    RetryOnMonthlyAnniversary = 1,

    /// <summary>
    /// Mark the membership as Expired and stop all further automatic attempts.
    /// Used by Lifestyle Ambassador (Annual) plans.
    /// </summary>
    MarkExpired = 2
}

/// <summary>Runtime status of a SubscriptionBillingState row.</summary>
public enum RecurringBillingStatus
{
    /// <summary>In good standing; next charge is on NextAttemptDate == NextBillingDate.</summary>
    Active = 1,

    /// <summary>A cycle attempt failed; currently working through the retry cadence.</summary>
    Retrying = 2,

    /// <summary>All cadence retries for a cycle failed; awaiting the next monthly anniversary.</summary>
    AwaitingAnniversaryRetry = 3,

    /// <summary>
    /// Engine is permanently stopped (90-day cap hit for Travel Advantage, or MarkExpired policy hit).
    /// Manual bill-now can revive this state to Active.
    /// </summary>
    Stopped = 4
}

/// <summary>Which funding source was used for a billing attempt.</summary>
public enum RecurringFundingSource
{
    CommissionBalance = 1,
    CreditCard = 2
}

/// <summary>Result of a single billing attempt.</summary>
public enum RecurringAttemptOutcome
{
    Success = 1,
    Failed = 2,

    /// <summary>
    /// The gateway accepted but the confirmation is asynchronous (60-min delayed fallback).
    /// The state is left untouched for the delayed Hangfire job to update.
    /// </summary>
    Scheduled = 3
}
