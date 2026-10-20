using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>Un paso del camino bajo una raiz: el miembro y el lado por el que cuelga de su padre.</summary>
public sealed record DualPathStep(string MemberId, TreeSide Side);

/// <summary>
/// Plan Q: el camino de cada nodo por debajo de <c>root</c> (sin root, con el nodo), de arriba abajo, por la clave
/// compacta (dbo.DualAncestorKeys y UX_DualTeamTree_BinaryPath). Sustituye a trocear HierarchyPath. En InMemory se sube
/// por ParentMemberId. La pierna de root en la que esta un nodo es el Side del primer paso.
/// </summary>
public static class DualAncestors
{
    private sealed record Fila(string NodeId, int Depth, string MemberId, int Side);

    // Las claves de los ascendientes de cada nodo pedido por debajo de @desde, las claves distintas buscadas una vez en
    // el indice unico de la clave y el reparto. @ids es un JSON con los MemberId (un solo parametro).
    private const string PathsSql = @"
SET NOCOUNT ON;
DROP TABLE IF EXISTS #k; DROP TABLE IF EXISTS #u;
SELECT n.MemberId AS NodeId, a.Depth, a.AncestorKey
INTO #k
FROM DualTeamTree n
CROSS APPLY dbo.DualAncestorKeys(n.BinaryPath, 0) a
WHERE n.IsDeleted = 0 AND n.BinaryPath IS NOT NULL AND a.Depth >= @desde
  AND n.MemberId IN (SELECT CAST(j.[value] AS nvarchar(450)) COLLATE DATABASE_DEFAULT FROM OPENJSON(@ids) j);
SELECT u.AncestorKey, d.MemberId, d.Side
INTO #u
FROM (SELECT DISTINCT AncestorKey FROM #k) u
JOIN DualTeamTree d ON u.AncestorKey = d.BinaryPath AND d.IsDeleted = 0;   -- (lectura: la clave a la izquierda, EscritoresDelBinarioTests)
SELECT k.NodeId, k.Depth, u.MemberId, u.Side FROM #k k JOIN #u u ON u.AncestorKey = k.AncestorKey;
DROP TABLE #k; DROP TABLE #u;";

    public static async Task<Dictionary<string, List<DualPathStep>>> PathsBelowAsync(
        AppDbContext db, DualTeamEntity root, IReadOnlyCollection<DualTeamEntity> nodes, CancellationToken ct = default)
    {
        var caminos = new Dictionary<string, List<DualPathStep>>(StringComparer.Ordinal);
        if (nodes.Count == 0) return caminos;

        if (db.Database.IsSqlServer())
        {
            var ids = JsonSerializer.Serialize(nodes.Select(n => n.MemberId).Distinct().ToList());
            var filas = await db.Database.SqlQueryRaw<Fila>(PathsSql,
                    new SqlParameter("@ids", SqlDbType.NVarChar, -1) { Value = ids },
                    new SqlParameter("@desde", (root.Depth ?? 0) + 1))
                .ToListAsync(ct);
            foreach (var g in filas.GroupBy(f => f.NodeId))
                caminos[g.Key] = g.OrderBy(f => f.Depth).Select(f => new DualPathStep(f.MemberId, (TreeSide)f.Side)).ToList();
            return caminos;
        }

        foreach (var node in nodes)
        {
            var camino = new List<DualPathStep>();
            var actual = node;
            while (actual is not null && actual.MemberId != root.MemberId)
            {
                camino.Add(new DualPathStep(actual.MemberId, actual.Side));
                var padre = actual.ParentMemberId;
                actual = padre is null ? null
                    : await db.DualTeamTree.AsNoTracking().FirstOrDefaultAsync(d => d.MemberId == padre, ct);
            }
            camino.Reverse();
            caminos[node.MemberId] = camino;
        }
        return caminos;
    }
}
