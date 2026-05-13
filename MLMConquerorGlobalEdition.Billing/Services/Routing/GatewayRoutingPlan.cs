namespace MLMConquerorGlobalEdition.Billing.Services.Routing;

/// <summary>
/// Ordered list of gateway attempts produced by the router.
/// Index 0 is the primary; subsequent entries are fallback steps.
/// </summary>
public class GatewayRoutingPlan
{
    public string                          RouteBucketKey { get; init; } = string.Empty;
    public IReadOnlyList<GatewayAttemptPlan> Steps        { get; init; } = Array.Empty<GatewayAttemptPlan>();
}
