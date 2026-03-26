using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;

public class CreateTicketRequest
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public TicketPriority Priority { get; set; }
}
