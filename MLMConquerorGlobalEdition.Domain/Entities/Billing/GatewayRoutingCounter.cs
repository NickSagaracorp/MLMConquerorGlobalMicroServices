using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// Persisted attempt counter — one row per CardProcessor per route bucket.
/// The deterministic % algorithm reads these rows to pick the processor
/// furthest below its configured target share.
/// Counter increment happens inside the same DB transaction as the charge,
/// so a rollback also rolls back the counter.
/// </summary>
public class GatewayRoutingCounter : AuditChangesLongKey
{
    /// <summary>
    /// Hash key that uniquely identifies the route bucket
    /// (derived from OperationType + CardBrand + match criterion).
    /// </summary>
    public string        RouteBucketKey { get; set; } = string.Empty;
    public CardProcessor CardProcessor  { get; set; }
    public long          AttemptCount   { get; set; }
}
