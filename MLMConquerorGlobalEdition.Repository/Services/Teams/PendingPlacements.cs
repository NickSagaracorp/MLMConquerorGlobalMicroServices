using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>Un miembro pendiente de colocar en el binario. Con inicializador (no constructor) para que se pueda filtrar despues.</summary>
public sealed class PendingPlacement
{
    public string MemberId { get; init; } = string.Empty;
    public string SponsorMemberId { get; init; } = string.Empty;
    public DateTime EnrollDate { get; init; }
}

/// <summary>
/// Plan P (decision de Nick, 2026-10-18): quien esta pendiente de colocar, en un unico sitio para todos los que
/// colocan en tanda (AutoPlacementJob, RunAutoPlacement y DevSeed place-bulk/force-place-all):
/// <list type="bullet">
/// <item>un miembro con patrocinador y sin nodo en el binario; o</item>
/// <item>una raiz provisional: tiene nodo, no tiene padre y su patrocinador SI esta en el binario. Se trata igual
/// que un miembro sin nodo, con el mismo plazo y las mismas reglas: al colocarla sube con todo su equipo.</item>
/// </list>
/// Las raices del sistema (sin patrocinador) no estan nunca pendientes. Orden: por fecha de alta, para que un
/// patrocinador pendiente se coloque antes que sus patrocinados (revision I2).
/// </summary>
public static class PendingPlacements
{
    /// <param name="enrolledUpTo">Solo los dados de alta hasta esta fecha, incluida (el plazo vencido); null = todos.</param>
    public static IQueryable<PendingPlacement> Query(AppDbContext db, DateTime? enrolledUpTo = null)
    {
        var miembros = db.MemberProfiles.AsNoTracking()
            .Where(m => !m.IsDeleted && m.SponsorMemberId != null);
        if (enrolledUpTo is not null)
            miembros = miembros.Where(m => m.EnrollDate <= enrolledUpTo.Value);

        return miembros
            .Where(m => !db.DualTeamTree.Any(d => d.MemberId == m.MemberId)
                        || (db.DualTeamTree.Any(d => d.MemberId == m.MemberId && d.ParentMemberId == null)
                            && db.DualTeamTree.Any(s => s.MemberId == m.SponsorMemberId)))
            .OrderBy(m => m.EnrollDate).ThenBy(m => m.MemberId)
            .Select(m => new PendingPlacement { MemberId = m.MemberId, SponsorMemberId = m.SponsorMemberId!, EnrollDate = m.EnrollDate });
    }
}
