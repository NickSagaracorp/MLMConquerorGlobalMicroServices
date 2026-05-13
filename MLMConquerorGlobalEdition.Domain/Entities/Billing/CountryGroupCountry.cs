using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// Maps an ISO-2 country code to a CountryGroup.
/// </summary>
public class CountryGroupCountry : AuditChangesIntKey
{
    public int    CountryGroupId  { get; set; }
    public string IsoCountryCode  { get; set; } = string.Empty; // ISO 3166-1 alpha-2

    public CountryGroup CountryGroup { get; set; } = null!;
}
