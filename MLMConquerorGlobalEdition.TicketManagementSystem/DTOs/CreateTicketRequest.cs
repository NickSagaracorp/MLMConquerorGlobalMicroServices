using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

public class CreateTicketRequest
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public TicketChannel Channel { get; set; } = TicketChannel.Portal;
    public string? Language { get; set; }
}
