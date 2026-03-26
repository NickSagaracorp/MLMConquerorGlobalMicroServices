using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tree;

public class GhostPointEntity : AuditChangesStringKey
{
    public string MemberId { get; set; } = string.Empty;
    public string LegMemberId { get; set; } = string.Empty;
    public TreeSide Side { get; set; }
    public decimal Points { get; set; }
    public string? AdminNote { get; set; }
    public bool IsActive { get; set; } = true;
}
