namespace MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

public class TicketCommentDto
{
    public long Id { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public DateTime CreationDate { get; set; }
}
