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

    /// <summary>
    /// Cuántos descendientes tiene <paramref name="rootMemberId"/>, contando como mucho hasta
    /// <paramref name="maxRows"/> + 1: null si pasa del tope (o si no es SQL Server). Es el dato barato con
    /// el que se decide el camino ANTES de materializar nada (revisión I-3): MemberStatistics.DualTeamSize
    /// no se mantiene (está a 0 en toda la base local). Un ciclo en el binario da filas sin fin, así que
    /// también cae del lado del tope (o de MAXRECURSION, error 530) y devuelve null: nunca un 500 (M-a).
    /// </summary>
    public static async Task<int?> CountUpToAsync(
        AppDbContext db, string rootMemberId, int maxRows, TreeSide? leg = null, CancellationToken ct = default)
    {
        if (!db.Database.IsSqlServer())
            return null;

        var tope = maxRows + 1;
        var lado = leg is null ? (int?)null : (int)leg.Value;
        try
        {
            var cuantos = await db.Database.SqlQuery<int>($"""
                WITH sub AS (
                    SELECT d.MemberId FROM DualTeamTree d
                    WHERE d.ParentMemberId = {rootMemberId} AND d.IsDeleted = 0
                      AND ({lado} IS NULL OR d.Side = {lado})
                    UNION ALL
                    SELECT c.MemberId FROM sub s
                    JOIN DualTeamTree c ON c.ParentMemberId = s.MemberId AND c.IsDeleted = 0
                )
                SELECT COUNT(*) AS [Value] FROM (SELECT TOP ({tope}) MemberId FROM sub) x
                OPTION (MAXRECURSION 32767)
                """).ToListAsync(ct);
            return cuantos.Count == 1 && cuantos[0] <= maxRows ? cuantos[0] : null;
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 530)
        {
            return null; // ciclo: MAXRECURSION agotado
        }
    }

    /// <summary>
    /// Deja los descendientes de <paramref name="rootMemberId"/> en una tabla temporal de la conexión del
    /// contexto, para que la rejilla se una a ellos en SQL sin devolver 20.000 ids a la aplicación ni
    /// mandarlos de vuelta como parámetro en cada consulta. Antes cuenta con <see cref="CountUpToAsync"/>:
    /// si pasa del tope (o hay un ciclo) devuelve null sin escribir nada y el llamador usa su camino de
    /// siempre. La tabla solo se lee a través del resultado y se borra al liberarlo (revisión M-b).
    /// </summary>
    public static async Task<MaterializedSubtree?> MaterializeAsync(
        AppDbContext db, string rootMemberId, int maxRows, TreeSide? leg = null, CancellationToken ct = default)
    {
        var cuantos = await CountUpToAsync(db, rootMemberId, maxRows, leg, ct);
        if (cuantos is null)
            return null;

        await db.Database.OpenConnectionAsync(ct);
        // La tabla se crea en un lote sin parámetros: dentro de sp_executesql (lo que usa EF con
        // parámetros) una tabla temporal desaparece al terminar la llamada. IGNORE_DUP_KEY: un id repetido
        // no da un 2627 (M-a).
        await db.Database.ExecuteSqlRawAsync(
            "DROP TABLE IF EXISTS #dual_subtree; CREATE TABLE #dual_subtree (MemberId nvarchar(450) NOT NULL PRIMARY KEY WITH (IGNORE_DUP_KEY = ON));", ct);
        var handle = new MaterializedSubtree(db, cuantos.Value);
        try
        {
            var tope = maxRows + 1;
            var lado = leg is null ? (int?)null : (int)leg.Value;
            await db.Database.ExecuteSqlAsync($"""
                WITH sub AS (
                    SELECT d.MemberId FROM DualTeamTree d
                    WHERE d.ParentMemberId = {rootMemberId} AND d.IsDeleted = 0
                      AND ({lado} IS NULL OR d.Side = {lado})
                    UNION ALL
                    SELECT c.MemberId FROM sub s
                    JOIN DualTeamTree c ON c.ParentMemberId = s.MemberId AND c.IsDeleted = 0
                )
                INSERT INTO #dual_subtree (MemberId)
                SELECT TOP ({tope}) MemberId FROM sub
                OPTION (MAXRECURSION 32767)
                """, ct);
            return handle;
        }
        catch
        {
            await handle.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Los descendientes que dejó <see cref="MaterializeAsync"/>. <see cref="Nodes"/> compone con LINQ;
    /// al liberarlo se borra la tabla temporal y se devuelve la conexión.
    /// </summary>
    public sealed class MaterializedSubtree : IAsyncDisposable
    {
        private readonly AppDbContext _db;
        private bool _disposed;

        internal MaterializedSubtree(AppDbContext db, int count)
        {
            _db   = db;
            Count = count;
        }

        public int Count { get; }

        public IQueryable<Domain.Entities.Tree.DualTeamEntity> Nodes =>
            _disposed
                ? throw new ObjectDisposedException(nameof(MaterializedSubtree))
                : _db.DualTeamTree.FromSqlRaw("SELECT d.* FROM DualTeamTree d JOIN #dual_subtree s ON s.MemberId = d.MemberId");

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            try { await _db.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS #dual_subtree;"); }
            finally { await _db.Database.CloseConnectionAsync(); }
        }
    }
}
