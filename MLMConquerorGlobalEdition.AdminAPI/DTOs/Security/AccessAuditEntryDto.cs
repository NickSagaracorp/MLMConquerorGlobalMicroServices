namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Security;

/// <summary>
/// Fila del registro de auditoría de acceso (cambios de estado de cuenta) para el grid de servidor del admin.
/// La lectura antigua devolvía la entidad entera; el grid necesita un DTO plano con nombres estables para que el
/// filtro y el orden de cada columna viajen al servidor. Los estados van como texto, que es lo que enseña la pantalla.
/// </summary>
public class AccessAuditEntryDto
{
    public long Id { get; set; }
    public string MemberId { get; set; } = string.Empty;
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string? Reason { get; set; }

    /// <summary>Quién hizo el cambio: el CreatedBy de la fila (la pantalla pedía un ChangedBy que nunca llegaba).</summary>
    public string ChangedBy { get; set; } = string.Empty;

    /// <summary>Cuándo cambió. Tiene índice propio, así que es el orden por defecto (más reciente primero).</summary>
    public DateTime ChangedAt { get; set; }
}
