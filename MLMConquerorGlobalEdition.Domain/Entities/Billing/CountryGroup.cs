using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// Named group of countries used as a routing criterion (Europe, LatinAmerica, RussiaBloc …).
/// </summary>
public class CountryGroup : AuditChangesIntKey
{
    public string Code { get; set; } = string.Empty; // e.g. "EUROPE"
    public string Name { get; set; } = string.Empty;

    public ICollection<CountryGroupCountry> Countries { get; set; } = new List<CountryGroupCountry>();
}
