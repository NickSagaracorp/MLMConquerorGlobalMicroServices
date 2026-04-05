using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Support;

public class TicketComment : AuditChangesLongKey
{
    public string TicketId { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorType { get; set; } = "customer";   // agent | customer | system
    public string Body { get; set; } = string.Empty;
    public bool IsInternal { get; set; }

    public Ticket Ticket { get; set; } = null!;
}
