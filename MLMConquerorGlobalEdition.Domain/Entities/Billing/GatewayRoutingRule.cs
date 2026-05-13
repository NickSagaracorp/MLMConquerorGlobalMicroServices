using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// Defines which processor(s) to use for a given (OperationType, CardBrand) combination
/// and match criterion (exact country / country-group / brand catch-all / global catch-all).
/// Specificity priority: exact country > country group > brand catch-all (IsoCountryCode=null,
/// CountryGroupId=null, IsCatchAll=false) > global catch-all (IsCatchAll=true).
/// </summary>
public class GatewayRoutingRule : AuditChangesIntKey
{
    public BillingOperationType OperationType { get; set; }
    public CardBrand?           CardBrand     { get; set; } // null = match all brands

    // ── Match criterion — exactly one should be set ───────────────────────
    public string? IsoCountryCode  { get; set; } // exact country match
    public int?    CountryGroupId  { get; set; } // country-group match
    public bool    IsCatchAll      { get; set; } // global fallback (no country/group constraint)

    // ── Currency ──────────────────────────────────────────────────────────
    public int? CurrencyPolicyId { get; set; }

    public bool IsActive { get; set; } = true;

    // ── Navigation ────────────────────────────────────────────────────────
    public CurrencyPolicy?                     CurrencyPolicy { get; set; }
    public CountryGroup?                       CountryGroup   { get; set; }
    public ICollection<GatewayRoutingRuleSplit> Splits        { get; set; } = new List<GatewayRoutingRuleSplit>();
}
