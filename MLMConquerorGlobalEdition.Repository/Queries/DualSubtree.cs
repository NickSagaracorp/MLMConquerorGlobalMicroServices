using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Descendientes de un nodo del árbol BINARIO (Plan M, M17–M20). El binario no cabe en hierarchyid a
/// escala (1.427 niveles como máximo; en local ya hay 934) y su ruta de texto no es indexable, así
/// que se baja por ParentMemberId con una CTE recursiva en SQL (índice que la cubre, M10). SQL en
/// línea, como LegPointMaintenance; una función de tabla no admitiría MAXRECURSION.
/// </summary>
public static class DualSubtree
{
    /// <summary>Por encima de esto el llamador sigue con su estrategia de siempre (raíz o equipos enormes).</summary>
    public const int MaxRowsForCte = 20_000;

    /// <summary>El máximo de SQL Server: con 0, un ciclo en los datos no terminaría nunca.</summary>
    public const int MaxRecursion = 32_767;

    public sealed class Row
    {
        public string MemberId { get; set; } = string.Empty;
        /// <summary>1 = hijo directo del nodo de partida.</summary>
        public int    Depth    { get; set; }
        /// <summary>TreeSide del hijo directo por el que se baja: la pierna del nodo de partida.</summary>
        public int    Leg      { get; set; }
    }

    /// <summary>
    /// Los descendientes de <paramref name="rootMemberId"/> (él no). Con <paramref name="leg"/>, solo los
    /// de esa pierna: la CTE arranca por un único hijo (los reportes binarios reducen primero por pierna,
    /// D4). Null si son más de <paramref name="maxRows"/> (TOP corta la recursión: saberlo cuesta
    /// maxRows + 1 filas) o si el proveedor no es SQL Server (InMemory en las pruebas): el llamador usa
    /// entonces su camino de siempre.
    /// </summary>
    public static async Task<IReadOnlyList<Row>?> DescendantsAsync(
        AppDbContext db, string rootMemberId, int maxRows, TreeSide? leg = null, CancellationToken ct = default)
    {
        if (!db.Database.IsSqlServer())
            return null;

        var tope  = maxRows + 1;
        var lado  = leg is null ? (int?)null : (int)leg.Value;
        // ToListAsync sin componer: EF manda el SQL tal cual (una CTE no se puede envolver en subconsulta).
        var filas = await db.Database.SqlQuery<Row>($"""
            WITH sub AS (
                SELECT d.MemberId, 1 AS Depth, CAST(d.Side AS int) AS Leg
                FROM DualTeamTree d
                WHERE d.ParentMemberId = {rootMemberId} AND d.IsDeleted = 0
                  AND ({lado} IS NULL OR d.Side = {lado})
                UNION ALL
                SELECT c.MemberId, s.Depth + 1, s.Leg
                FROM sub s
                JOIN DualTeamTree c ON c.ParentMemberId = s.MemberId AND c.IsDeleted = 0
            )
            SELECT TOP ({tope}) MemberId, Depth, Leg FROM sub
            OPTION (MAXRECURSION 32767)
            """).ToListAsync(ct);

        return filas.Count > maxRows ? null : filas;
    }
}
