using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Services.Routing;

/// <summary>
/// A single step in the routing plan (primary at FallbackStepIndex=0, then fallbacks).
/// </summary>
public class GatewayAttemptPlan
{
    public CardProcessor CardProcessor       { get; init; }
    public string        PresentmentCurrency { get; init; } = "USD";
    public decimal       Amount              { get; init; }
    public int           FallbackStepIndex   { get; init; }

    /// <summary>Minutes to wait before executing this step (0 = immediate).</summary>
    public int           DelayMinutes        { get; init; }
}
