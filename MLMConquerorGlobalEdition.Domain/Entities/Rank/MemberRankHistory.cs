using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Rank;

public class MemberRankHistory : AuditChangesStringKey
{
    public string MemberId { get; set; } = string.Empty;
    public int RankDefinitionId { get; set; }
    public int? PreviousRankId { get; set; }
    public DateTime AchievedAt { get; set; }
    public string? GeneratedCertificateUrl { get; set; }

    public RankDefinition? RankDefinition { get; set; }
}
