using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Commission;

public class CommissionOperationType : AuditChangesIntKey
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
