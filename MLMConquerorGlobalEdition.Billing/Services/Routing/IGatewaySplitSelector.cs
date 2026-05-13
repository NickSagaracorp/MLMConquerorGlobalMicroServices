using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Routing;

public interface IGatewaySplitSelector
{
    /// <summary>
    /// Picks the CardProcessor for this route bucket using the persisted deterministic
    /// counter algorithm (largest deficit wins). Also increments the counter for the
    /// chosen processor. Must be called inside the same transaction as the charge.
    /// </summary>
    Task<Result<CardProcessor>> PickAsync(
        string routeBucketKey,
        IReadOnlyList<GatewayRoutingRuleSplit> splits,
        CancellationToken ct = default);
}
