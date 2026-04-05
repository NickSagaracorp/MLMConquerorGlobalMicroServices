using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Support;

public class KnowledgeBaseArticle : AuditChangesStringKey
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;            // unique
    public string Body { get; set; } = string.Empty;            // markdown
    public int CategoryId { get; set; }
    public string TagsJson { get; set; } = "[]";
    public KbVisibility Visibility { get; set; } = KbVisibility.Draft;
    public string AuthorAgentId { get; set; } = string.Empty;
    public int ViewCount { get; set; }
    public int HelpfulCount { get; set; }
    public int NotHelpfulCount { get; set; }
    public string? SourceTicketId { get; set; }                 // created from a resolved ticket
    public DateTime? PublishedAt { get; set; }
    public int Version { get; set; } = 1;

    public TicketCategory Category { get; set; } = null!;
    public ICollection<KbArticleVersion> Versions { get; set; } = new List<KbArticleVersion>();
}
