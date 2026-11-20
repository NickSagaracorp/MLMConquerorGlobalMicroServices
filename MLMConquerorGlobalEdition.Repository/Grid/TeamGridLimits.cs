using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Grid;

/// <summary>
/// Por encima de <see cref="FreeMaxRows"/>, Mi equipo solo ordena por estructura (Node o
/// BinaryPath, un recorrido del indice) y solo busca por nombre o id por prefijo (MemberNameSearch). Ordenar o buscar por
/// subcadena un rango de un millon de filas tarda de 2 a 18 s.
/// </summary>
public static class TeamGridLimits
{
    public const int FreeMaxRows = 20_000;

    /// <summary>True si el equipo supera el limite: quita los ordenes pedidos y quien llama ordena por estructura.</summary>
    public static bool Limit(GridDataRequest request, int teamSize)
    {
        if (teamSize <= FreeMaxRows) return false;
        request.Sorts = new List<GridSort>();
        return true;
    }

    /// <summary>
    /// El tamano guardado del equipo de inscripcion de <paramref name="memberId"/>
    /// (MemberStatistics.EnrollmentTeamSize: activados no cancelados, diferido por la cola de deltas). Basta para decidir el
    /// limite; no es el total de filas de una rejilla (para eso esta GenealogySubtree.CountUnderAsync).
    /// </summary>
    public static Task<int> EnrollmentTeamSizeAsync(AppDbContext db, string memberId, CancellationToken ct = default) =>
        db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == memberId)
            .OrderBy(s => s.Id).Select(s => s.EnrollmentTeamSize).FirstOrDefaultAsync(ct);
}
