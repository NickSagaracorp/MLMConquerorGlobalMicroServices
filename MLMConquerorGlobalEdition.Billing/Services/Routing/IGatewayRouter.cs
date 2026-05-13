using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Routing;

public interface IGatewayRouter
{
    Task<Result<GatewayRoutingPlan>> ResolveAsync(
        GatewayRoutingContext ctx,
        CancellationToken ct = default);
}
