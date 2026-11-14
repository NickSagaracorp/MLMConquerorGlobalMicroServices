using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Recognition;

/// <summary>
/// Resultado de reconocimiento que no es un mes de CEO Call ni un evento (p. ej. «Recognized Higher Level»,
/// «Do not recognize»). Sirve para las dos columnas. Con <see cref="RequiresReason"/>, el admin escribe el motivo y la
/// vista muestra «Nombre – motivo».
/// </summary>
public class RecognitionOption : AuditChangesIntKey
{
    public string Name           { get; set; } = string.Empty;
    public bool   RequiresReason { get; set; }
    public int    SortOrder      { get; set; }
    public bool   IsActive       { get; set; } = true;
}
