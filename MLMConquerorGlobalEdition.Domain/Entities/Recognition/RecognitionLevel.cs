using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Recognition;

/// <summary>
/// Nivel de Club Recognition (parámetro del admin): quien gana <see cref="Threshold"/> o más en la ventana de
/// <see cref="WindowDays"/> días (null = de por vida) lo alcanza. Las ganancias siguen la regla de los reportes Earnings.
/// Se desactiva, no se borra: los registros que ya lo alcanzaron lo referencian.
/// </summary>
public class RecognitionLevel : AuditChangesIntKey
{
    public string  Name            { get; set; } = string.Empty;
    public decimal Threshold       { get; set; }
    public int?    WindowDays      { get; set; }
    public string? Reward          { get; set; }
    public bool    RewardNeedsSize { get; set; }
    public int     SortOrder       { get; set; }
    public bool    IsActive        { get; set; } = true;
}
