using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// Durable snapshot of an exchange rate fetched from currencyconverterapi.com.
/// Used as a fallback when Redis is unavailable.
/// BaseCurrency is always USD.
/// </summary>
public class ExchangeRateSnapshot : AuditChangesLongKey
{
    public string   BaseCurrency  { get; set; } = "USD";
    public string   QuoteCurrency { get; set; } = string.Empty;
    public decimal  Rate          { get; set; }
    public DateTime FetchedAtUtc  { get; set; }
    public DateTime ExpiresAtUtc  { get; set; }
}
