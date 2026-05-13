using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Recurring;

public interface IRecurringBillingProcessor
{
    /// <summary>
    /// Orchestrates one due billing attempt for the given SubscriptionBillingState.
    /// Handles commission-balance funding, card charging, state advancement, and attempt logging.
    /// Transactional: all mutations within a single SaveChangesAsync call.
    /// </summary>
    /// <param name="billingStateId">Id of the SubscriptionBillingState to process.</param>
    /// <param name="forceBillNow">
    /// When true, ignores NextAttemptDate (used by the admin bill-now action).
    /// Also revives a Stopped state to Active before processing.
    /// </param>
    Task<Result<RecurringBillingProcessorResult>> ProcessAsync(
        string billingStateId,
        bool forceBillNow = false,
        CancellationToken ct = default);
}

public class RecurringBillingProcessorResult
{
    public string BillingStateId { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty; // "Success" | "Failed" | "Scheduled" | "Skipped"
    public string? FundingSource { get; init; }
    public string? PaymentHistoryId { get; init; }
    public string? FailureReason { get; init; }
}
