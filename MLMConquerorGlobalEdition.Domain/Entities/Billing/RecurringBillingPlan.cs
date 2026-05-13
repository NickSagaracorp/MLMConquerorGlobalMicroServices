using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// Configuration record that governs a recurring billing cycle for one or more products.
/// One plan can cover many products (e.g. Travel Advantage covers Elite/VIP/Turbo).
/// </summary>
public class RecurringBillingPlan : AuditChangesIntKey
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Every30Days or AnnualFromLastBilling.</summary>
    public RecurringCycleType CycleType { get; set; }

    /// <summary>
    /// Comma-separated list of day offsets for retry attempts after the first failure.
    /// E.g. "1,2,2,2,2,2" for Travel Advantage or "1,1,1,2,2,5,5" for Lifestyle Ambassador.
    /// The first attempt of a new cycle is always on NextBillingDate; each subsequent offset
    /// is measured from the previous attempt's date.
    /// </summary>
    public string RetryCadenceDays { get; set; } = string.Empty;

    /// <summary>What to do when all cadence retries are exhausted.</summary>
    public RecurringFailurePolicy OnAllRetriesFail { get; set; }

    /// <summary>
    /// If set, stop billing and set membership to HoldByBilling when this many days have elapsed
    /// since the last successful billing (or BillingAnchorDate if never billed). Null = no auto-stop.
    /// </summary>
    public int? StopAfterUnbilledDays { get; set; }

    /// <summary>
    /// When true, attempt commission balance payment before falling back to the credit card.
    /// </summary>
    public bool PayFromCommissionBalanceFirst { get; set; }

    /// <summary>
    /// The TokenType issued when a commission-balance payment succeeds.
    /// Required when PayFromCommissionBalanceFirst is true.
    /// Stored at plan level when a single token type covers all products in the plan;
    /// overridden per product via RecurringBillingPlanProduct.TokenTypeIdOverride when products differ.
    /// </summary>
    public int? TokenTypeId { get; set; }

    /// <summary>
    /// When set, charge exactly this amount regardless of Product.MonthlyFee / AnnualPrice.
    /// Null = derive from the product price for the cycle type.
    /// </summary>
    public decimal? FixedAmountOverride { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<RecurringBillingPlanProduct> PlanProducts { get; set; } = new List<RecurringBillingPlanProduct>();

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Returns the parsed cadence array as an array of ints.</summary>
    public int[] ParseCadence()
    {
        if (string.IsNullOrWhiteSpace(RetryCadenceDays))
            return Array.Empty<int>();

        return RetryCadenceDays
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToArray();
    }
}
