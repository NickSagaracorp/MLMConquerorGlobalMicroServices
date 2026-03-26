using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Interfaces;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tree;

public class GenealogyEntity : AuditChangesStringKey, ITreeNode
{
    public string MemberId { get; set; } = string.Empty;
    public string? ParentMemberId { get; set; }
    public string HierarchyPath { get; set; } = string.Empty;
    public int Level { get; set; }
}
