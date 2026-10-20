using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tree;

/// <summary>Plan Q (Bloque 3): sin ruta de texto; la posicion es ParentMemberId, Side y la clave compacta (BinaryPath, Depth).</summary>
public class DualTeamEntity : AuditChangesStringKey
{
    public string MemberId { get; set; } = string.Empty;
    public string? ParentMemberId { get; set; }
    public TreeSide Side { get; set; }
    public decimal LeftLegPoints { get; set; }
    public decimal RightLegPoints { get; set; }

    /// <summary>Plan P: clave compacta (DualBinaryPath). La escribe solo DualTeamNodes.</summary>
    public byte[]? BinaryPath { get; set; }
    /// <summary>Plan P: niveles bajo su raiz (0 = raiz).</summary>
    public int? Depth { get; set; }
    /// <summary>Plan P: nodos en cada pierna (mantenidos como los puntos, por LegPointMaintenance).</summary>
    public int LeftLegCount { get; set; }
    public int RightLegCount { get; set; }

    /// <summary>
    /// Plan Q (DQ13): suscripciones activas del propio miembro YA APLICADAS por la cola. Es lo que viaja con el nodo al
    /// moverlo o retirarlo; no forma parte de sus piernas.
    /// </summary>
    public int SelfQualified { get; set; }
    /// <summary>Plan Q (DQ13): suscripciones activas de cada pierna, mantenidas por LegPointMaintenance como los puntos.</summary>
    public int LeftLegQualified { get; set; }
    public int RightLegQualified { get; set; }
}
