using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// One row per CardProcessor. Defines display name and capabilities.
/// Authoritative catalog — seeded on first run.
/// </summary>
public class PaymentGatewayCatalog : AuditChangesIntKey
{
    public CardProcessor Processor      { get; set; }
    public string        DisplayName    { get; set; } = string.Empty;
    public bool          IsActive       { get; set; } = true;
    public bool          SupportsRefund    { get; set; }
    public bool          SupportsRecurring { get; set; }
}
