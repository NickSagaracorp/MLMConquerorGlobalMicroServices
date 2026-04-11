using MLMConquerorGlobalEdition.Domain.Entities.Orders;

namespace MLMConquerorGlobalEdition.Domain.Entities.General;

/// <summary>
/// Maps which products are available for signup in a given country.
/// When no mappings exist for a country, all JoinPageMembership products are available (fallback).
/// </summary>
public class CountryProduct : AuditChangesIntKey
{
    public int CountryId { get; set; }
    public string ProductId { get; set; } = string.Empty;

    /// <summary>Soft-disable a mapping without deleting it.</summary>
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Country? Country { get; set; }
    public virtual Product? Product { get; set; }
}
