using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tree;

/// <summary>
/// Plan Q (P-R9): un cambio pendiente de las piernas del binario. Se suma a la pierna <see cref="Side"/> de
/// <see cref="StartMemberId"/> y, por el lado por el que se baja, a cada ascendiente que ese nodo tenga CUANDO SE APLICA.
/// Lo escribe y lo aplica LegPointMaintenance; ApplyDualLegDeltasJob vacia la cola y la borra.
/// </summary>
public class DualLegDelta
{
    public long Id { get; set; }
    public string StartMemberId { get; set; } = string.Empty;
    public TreeSide Side { get; set; }
    public decimal Points { get; set; }
    public int NodeCount { get; set; }
    /// <summary>Plan Q (DQ13): cambio de suscripciones activas.</summary>
    public int QualifiedCount { get; set; }
    /// <summary>
    /// Plan Q (DQ13): el cambio es DEL PROPIO StartMemberId (una suscripcion suya se activo o dejo de estarlo): va a su
    /// SelfQualified y a las piernas de sus ascendientes, no a una pierna suya; Side no se usa. Sin nodo vivo, espera en la
    /// cola a que lo tenga. Lo escribe solo QualifiedTeamDeltas (y la retirada, con lo ya aplicado).
    /// </summary>
    public bool OwnChange { get; set; }
    public DateTime CreationDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
