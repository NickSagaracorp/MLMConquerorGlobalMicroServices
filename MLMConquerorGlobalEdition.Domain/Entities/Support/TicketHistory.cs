using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Support;

public class TicketHistory : AuditChangesLongKey
{
    public string TicketId { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;         // status | priority | assignedTo | escalationLevel | etc.
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string ChangedByType { get; set; } = "system";     // agent | customer | system
    public string? ChangedById { get; set; }
    public string? ChangeReason { get; set; }

    public Ticket Ticket { get; set; } = null!;
}
