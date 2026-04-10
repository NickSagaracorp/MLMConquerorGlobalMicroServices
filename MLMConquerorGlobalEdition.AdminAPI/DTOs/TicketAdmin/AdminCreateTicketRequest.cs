using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;

public class AdminCreateTicketRequest
{
    public string MemberId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
}
