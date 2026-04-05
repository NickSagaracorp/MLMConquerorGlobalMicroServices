using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

public class KbArticleSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string Visibility { get; set; } = string.Empty;
    public int ViewCount { get; set; }
    public int HelpfulCount { get; set; }
    public int NotHelpfulCount { get; set; }
    public int Version { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreationDate { get; set; }
}

public class KbArticleDto : KbArticleSummaryDto
{
    public string Body { get; set; } = string.Empty;
    public string TagsJson { get; set; } = string.Empty;
    public string AuthorAgentId { get; set; } = string.Empty;
    public string? SourceTicketId { get; set; }
}

public class CreateKbArticleRequest
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string TagsJson { get; set; } = "[]";
    public KbVisibility Visibility { get; set; } = KbVisibility.Draft;
    public string? SourceTicketId { get; set; }
    public string? ChangeNote { get; set; }
}

public class KbFeedbackRequest
{
    public bool IsHelpful { get; set; }
}
