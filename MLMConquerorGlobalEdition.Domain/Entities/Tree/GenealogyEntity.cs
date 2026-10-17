using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Interfaces;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tree;

public class GenealogyEntity : AuditChangesStringKey, ITreeNode
{
    public string MemberId { get; set; } = string.Empty;
    public string? ParentMemberId { get; set; }
    public string HierarchyPath { get; set; } = string.Empty;
    public int Level { get; set; }

    /// <summary>
    /// Posición en el árbol de inscripción (hierarchyid, Plan M). El equipo de un miembro es
    /// Node.IsDescendantOf(su Node). Lo escribe GenealogyNodes.NextChildAsync; nulo solo mientras dura
    /// el relleno inicial (Scripts/sql/plan-m/02-relleno-nodo-genealogia.sql).
    /// </summary>
    public HierarchyId? Node { get; set; }
}
