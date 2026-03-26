using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Support;

public class Ticket : AuditChangesStringKey
{
    public string MemberId { get; set; } = string.Empty;
    public string? AssignedToUserId { get; set; }
    public int CategoryId { get; set; }
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? MergedIntoTicketId { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public TicketCategory? Category { get; set; }
    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
}
