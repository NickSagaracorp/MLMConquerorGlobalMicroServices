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
    public DateTime CreationDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
