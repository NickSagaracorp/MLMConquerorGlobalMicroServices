using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;

public class UpdateTicketRequest
{
    public TicketStatus? Status { get; set; }
    public TicketPriority? Priority { get; set; }
    public string? AssignedToUserId { get; set; }
}
