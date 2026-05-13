using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// Full chain-of-custody audit record for every charge attempt (primary + fallbacks).
/// </summary>
public class GatewayChargeAttempt : AuditChangesLongKey
{
    public string        RouteBucketKey        { get; set; } = string.Empty;
    public CardProcessor CardProcessor         { get; set; }

    /// <summary>0 = primary attempt; 1+ = fallback step index.</summary>
    public int           FallbackStepIndex     { get; set; }

    public string        PresentmentCurrency   { get; set; } = "USD";
    public decimal       OriginalAmountUsd     { get; set; }
    public decimal       ConvertedAmount       { get; set; }
    public decimal?      ExchangeRateUsed      { get; set; }

    /// <summary>"Success" | "Failed" | "Scheduled"</summary>
    public string        Outcome               { get; set; } = string.Empty;

    public string?       GatewayTransactionId  { get; set; }
    public string?       PaymentHistoryId      { get; set; }
    public string?       FailureReason         { get; set; }

    public DateTime      AttemptedAtUtc        { get; set; }
    public DateTime?     CompletedAtUtc        { get; set; }

    /// <summary>
    /// Round-trip latency of the gateway call in milliseconds.
    /// Recorded by GatewayChargeOrchestrator after each attempt.
    /// Used by RecurringBillingPlanner (§10.3) to size the worker pool.
    /// Null when the outcome is "Scheduled" (async/deferred path — no round-trip measured).
    /// </summary>
    public long?         LatencyMs             { get; set; }

    // ── Context ───────────────────────────────────────────────────────────
    public string        MemberId              { get; set; } = string.Empty;
    public BillingOperationType OperationType  { get; set; }
    public CardBrand     CardBrand             { get; set; }
}
