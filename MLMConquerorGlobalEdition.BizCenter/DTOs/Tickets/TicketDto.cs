namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;

public class TicketDto
{
    public string Id { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
    public string? AssignedToUserId { get; set; }
}
