using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// One processor entry within a routing rule, with its target volume percentage.
/// Validator ensures all splits for a rule sum to 100.
/// </summary>
public class GatewayRoutingRuleSplit : AuditChangesIntKey
{
    public int           GatewayRoutingRuleId { get; set; }
    public CardProcessor CardProcessor        { get; set; }

    /// <summary>Target percentage (0-100). Splits for a rule must sum to 100.</summary>
    public decimal WeightPercent { get; set; }

    /// <summary>Determines tie-break order when two processors have equal deficit.</summary>
    public int SortOrder { get; set; }

    public GatewayRoutingRule GatewayRoutingRule { get; set; } = null!;
}
