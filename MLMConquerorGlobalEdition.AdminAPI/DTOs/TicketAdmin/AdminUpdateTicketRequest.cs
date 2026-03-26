using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;

public class AdminUpdateTicketRequest
{
    public TicketStatus? Status { get; set; }
    public TicketPriority? Priority { get; set; }
}
