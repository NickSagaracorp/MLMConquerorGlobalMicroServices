using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// Defines the presentment currency for a target market and the FX markup applied.
/// E.g. PresentmentCurrency=EUR, MarkupPercent=2 means European charges are converted
/// to EUR with a 2 % margin before sending to the gateway.
/// </summary>
public class CurrencyPolicy : AuditChangesIntKey
{
    /// <summary>ISO 4217 currency code, e.g. "EUR", "CAD", "AUD", "GBP", "USD".</summary>
    public string  PresentmentCurrency { get; set; } = string.Empty;

    /// <summary>Percentage markup applied on top of the spot rate. Default 2.</summary>
    public decimal MarkupPercent       { get; set; } = 2m;

    public bool IsActive { get; set; } = true;

    public string? Description { get; set; }
}
