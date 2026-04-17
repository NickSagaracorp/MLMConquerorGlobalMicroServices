using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.General;

public class Country : AuditChangesIntKey
{
    public string Iso2 { get; set; } = string.Empty;
    public string Iso3 { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameNative { get; set; } = string.Empty;
    public string DefaultLanguageCode { get; set; } = string.Empty;
    public string FlagEmoji { get; set; } = string.Empty;
    public string? PhoneCode { get; set; }
    public bool IsActive { get; set; } = false;
    public int SortOrder { get; set; } = 0;

    public int? RegionId { get; set; }
    public Region? Region { get; set; }
}
