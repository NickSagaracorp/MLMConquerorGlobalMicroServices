using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Member;

/// <summary>
/// Foto diaria de miembros por país (reporte Top Countries): cuántos miembros había al cierre de <see cref="SnapshotDate"/>
/// por país, tipo y estado. La escribe MemberCountrySnapshotWriter (job nocturno del BizCenter o foto manual del admin);
/// recalcular un día reemplaza sus filas. No se reconstruye el pasado: la primera foto es la del día en que se activa.
/// </summary>
public class MemberCountrySnapshot : AuditChangesLongKey
{
    /// <summary>El día de la foto (columna date, hora del servidor).</summary>
    public DateTime SnapshotDate { get; set; }

    /// <summary>El mismo valor que MemberProfile.Country.</summary>
    public string Country { get; set; } = string.Empty;

    public MemberType MemberType { get; set; }
    public MemberAccountStatus Status { get; set; }
    public int MemberCount { get; set; }
}
