using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Interfaces;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tree;

public class DualTeamEntity : AuditChangesStringKey, ITreeNode
{
    public string MemberId { get; set; } = string.Empty;
    public string? ParentMemberId { get; set; }
    public TreeSide Side { get; set; }
    public string HierarchyPath { get; set; } = string.Empty;
    public decimal LeftLegPoints { get; set; }
    public decimal RightLegPoints { get; set; }
}
