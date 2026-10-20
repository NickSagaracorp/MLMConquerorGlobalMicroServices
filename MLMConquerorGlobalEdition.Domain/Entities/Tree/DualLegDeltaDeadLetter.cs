using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tree;

/// <summary>
/// Revision del bloque 2 (M-2 y M-3): un delta de pierna que el vaciado aparto de la cola sin aplicarlo, con el motivo.
/// <list type="bullet">
/// <item><c>sin-nodo</c>: un delta DE PIERNA cuyo arranque no tiene nodo vivo con clave (una retirada borra los suyos, asi
/// que es una anomalia); antes se descartaba en silencio.</item>
/// <item><c>fallo</c>: una tanda de solo ese arranque fallo <c>LegPointMaintenance.MaxAttempts</c> veces (veneno); el
/// error va en <see cref="Reason"/>.</item>
/// </list>
/// Para reintentarlo, se vuelve a insertar en DualLegDeltas a mano (con los servicios parados) y se borra de aqui.
/// </summary>
public class DualLegDeltaDeadLetter
{
    public long Id { get; set; }
    public long DeltaId { get; set; }
    public string StartMemberId { get; set; } = string.Empty;
    public TreeSide Side { get; set; }
    public decimal Points { get; set; }
    public int NodeCount { get; set; }
    public int QualifiedCount { get; set; }
    public bool OwnChange { get; set; }
    public int Attempts { get; set; }
    public DateTime CreationDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime DiscardedDate { get; set; }
    public string Reason { get; set; } = string.Empty;
}
