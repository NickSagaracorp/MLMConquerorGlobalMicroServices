using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tree;

public class PlacementLog : AuditChangesLongKey
{
    public string MemberId { get; set; } = string.Empty;
    public string PlacedUnderMemberId { get; set; } = string.Empty;
    public TreeSide Side { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public int UnplacementCount { get; set; }
    public DateTime? FirstPlacementDate { get; set; }
}
