namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;

public class AdminTicketDto
{
    public string Id { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? AssignedToUserId { get; set; }
    public DateTime CreationDate { get; set; }
    public int CommentCount { get; set; }
}
