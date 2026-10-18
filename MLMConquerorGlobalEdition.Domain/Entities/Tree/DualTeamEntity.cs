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

    /// <summary>Plan P: clave compacta (DualBinaryPath). La escribe solo DualTeamNodes.</summary>
    public byte[]? BinaryPath { get; set; }
    /// <summary>Plan P: niveles bajo su raiz (0 = raiz).</summary>
    public int? Depth { get; set; }
    /// <summary>Plan P: nodos en cada pierna (mantenidos como los puntos, por LegPointMaintenance).</summary>
    public int LeftLegCount { get; set; }
    public int RightLegCount { get; set; }
}
