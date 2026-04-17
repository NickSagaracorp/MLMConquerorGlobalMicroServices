namespace MLMConquerorGlobalEdition.Domain.Entities.General;

public class Region : AuditChangesIntKey
{
    public string Name        { get; set; } = string.Empty;
    public string Code        { get; set; } = string.Empty; // e.g. "LATAM", "NA", "EU"
    public string? Description { get; set; }
    public bool   IsActive    { get; set; } = true;
    public int    SortOrder   { get; set; } = 0;

    public ICollection<Country>       Countries { get; set; } = new List<Country>();
    public ICollection<RegionGateway> Gateways  { get; set; } = new List<RegionGateway>();
}
