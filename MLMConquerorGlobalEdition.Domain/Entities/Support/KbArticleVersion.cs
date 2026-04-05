using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Support;

public class KbArticleVersion : AuditChangesLongKey
{
    public string ArticleId { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string BodySnapshot { get; set; } = string.Empty;
    public string EditedByAgentId { get; set; } = string.Empty;
    public string? ChangeNote { get; set; }

    public KnowledgeBaseArticle Article { get; set; } = null!;
}
