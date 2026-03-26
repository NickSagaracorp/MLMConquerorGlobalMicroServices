using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Support;

public class TicketCategory : AuditChangesIntKey
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
