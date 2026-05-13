using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// Join table: links a RecurringBillingPlan to the Products it governs.
/// Allows a plan to cover multiple products (e.g. Travel Advantage Elite/VIP/Turbo).
/// </summary>
public class RecurringBillingPlanProduct : AuditChangesIntKey
{
    public int RecurringBillingPlanId { get; set; }
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// Per-product token type override. When set, this token type is issued instead of
    /// RecurringBillingPlan.TokenTypeId when commission-balance funds a bill for this specific product.
    /// Null = use the plan-level TokenTypeId.
    /// </summary>
    public int? TokenTypeIdOverride { get; set; }

    // Navigation
    public RecurringBillingPlan? Plan { get; set; }
}
