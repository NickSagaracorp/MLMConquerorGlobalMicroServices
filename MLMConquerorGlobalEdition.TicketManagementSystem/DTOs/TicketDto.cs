namespace MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

public class TicketDto
{
    public string Id { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string MemberId { get; set; } = string.Empty;
    public string? AssignedToUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreationDate { get; set; }
    public int CommentCount { get; set; }
}
