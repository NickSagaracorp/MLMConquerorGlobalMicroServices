using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// Immutable audit log of every recurring billing attempt (success and failure).
/// One row per attempt; never modified after insertion.
/// </summary>
public class RecurringBillingAttempt : AuditChangesLongKey
{
    public string SubscriptionBillingStateId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;

    /// <summary>Attempt number within the current cycle (0 = first attempt, 1 = first retry, ...).</summary>
    public int AttemptIndex { get; set; }

    public DateTime AttemptedAt { get; set; }
    public decimal Amount { get; set; }

    public RecurringFundingSource FundingSource { get; set; }
    public RecurringAttemptOutcome Outcome { get; set; }

    // ── Cross-references to related rows (all nullable — not all paths produce all rows) ──

    public string? PaymentHistoryId { get; set; }
    public string? OrderId { get; set; }
    public long? TokenTransactionId { get; set; }

    /// <summary>The CommissionEarning row with Amount = -amountDue (the debit) created on commission-funded success.</summary>
    public string? CommissionDeductionEarningId { get; set; }

    /// <summary>The GatewayChargeAttempt row from the routing engine (card path only).</summary>
    public long? GatewayChargeAttemptId { get; set; }

    public string? FailureReason { get; set; }
}
