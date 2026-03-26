using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Rank;

public class RankDefinition : AuditChangesIntKey
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public RankDefinitionStatus Status { get; set; } = RankDefinitionStatus.Active;
    public string? CertificateTemplateUrl { get; set; }

    public ICollection<RankRequirement> Requirements { get; set; } = new List<RankRequirement>();
}
