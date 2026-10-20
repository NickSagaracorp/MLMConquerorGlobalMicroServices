using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>Un paso del camino bajo una raiz: el miembro y el lado por el que cuelga de su padre.</summary>
public sealed record DualPathStep(string MemberId, TreeSide Side);

/// <summary>
/// Plan Q: el camino de cada nodo por debajo de <c>root</c> (sin root, con el nodo), de arriba abajo, por la clave
/// compacta (dbo.DualPathKeys y UX_DualTeamTree_BinaryPath). Sustituye a trocear HierarchyPath. En InMemory se sube
/// por ParentMemberId. La pierna de root en la que esta un nodo es el Side del primer paso.
/// </summary>
public static class DualAncestors
{
    private sealed record Fila(string NodeId, int Depth, string MemberId, int Side);

    // Revision del bloque 3: solo los ascendientes DISTINTOS de los nodos pedidos por debajo de la raiz. Los nodos van
    // en el orden de la clave y cada uno pide sus ascendientes desde @desde o desde el ultimo que comparte con el
    // anterior (dbo.DualPathKeys con su @desde), asi que ningun ascendiente se calcula, se busca ni se devuelve dos
    // veces: 100 hallazgos a 8.500 niveles en una misma pierna son unas 8.500 filas, no 850.000. Cada fila va con el
    // nodo que la pidio (NodeId) y su profundidad; el camino de cada nodo se arma en memoria. @pedidos es un JSON
    // [{"id":..,"d":..}] (un solo parametro).
    private const string PathsSql = @"
SELECT j.id AS NodeId, a.Depth, d.MemberId, d.Side
FROM OPENJSON(@pedidos) WITH (id nvarchar(450) '$.id', desde int '$.d') j
JOIN DualTeamTree n ON n.MemberId = j.id COLLATE DATABASE_DEFAULT AND n.IsDeleted = 0 AND n.BinaryPath IS NOT NULL
CROSS APPLY dbo.DualPathKeys(n.BinaryPath, 0, j.desde) a
JOIN DualTeamTree d ON a.AncestorKey = d.BinaryPath AND d.IsDeleted = 0";   // (lectura: la clave a la izquierda, EscritoresDelBinarioTests)

    private sealed record Pedido(string Id, int D, int Comun);

    public static async Task<Dictionary<string, List<DualPathStep>>> PathsBelowAsync(
        AppDbContext db, DualTeamEntity root, IReadOnlyCollection<DualTeamEntity> nodes, CancellationToken ct = default)
    {
        var caminos = new Dictionary<string, List<DualPathStep>>(StringComparer.Ordinal);
        if (nodes.Count == 0) return caminos;

        if (db.Database.IsSqlServer())
        {
            var desde = (root.Depth ?? 0) + 1;
            // En el orden de la clave, el ascendiente comun mas hondo con cualquier nodo anterior es el que se comparte
            // con el inmediatamente anterior (un subarbol es un rango contiguo).
            var ordenados = nodes.Where(n => n.BinaryPath is not null)
                .GroupBy(n => n.MemberId, StringComparer.Ordinal).Select(g => g.First())
                .OrderBy(n => n.BinaryPath, Comparer<byte[]?>.Create(DualBinaryPath.SqlCompare)).ToList();
            var pedidos = new List<Pedido>(ordenados.Count);
            for (var i = 0; i < ordenados.Count; i++)
            {
                var comun = i == 0 ? -1 : DualBinaryPath.CommonDepth(ordenados[i - 1].BinaryPath, ordenados[i].BinaryPath);
                pedidos.Add(new Pedido(ordenados[i].MemberId, Math.Max(desde, comun + 1), comun));
            }
            var json = JsonSerializer.Serialize(pedidos.Select(p => new { id = p.Id, d = p.D }));
            var filas = await db.Database.SqlQueryRaw<Fila>(PathsSql,
                    new SqlParameter("@pedidos", SqlDbType.NVarChar, -1) { Value = json })
                .ToListAsync(ct);
            var propios = filas.ToLookup(f => f.NodeId, StringComparer.Ordinal);

            // El camino de cada nodo: lo comun con el anterior (hasta su profundidad) mas lo que pidio el propio nodo.
            List<DualPathStep>? anterior = null;
            foreach (var p in pedidos)
            {
                var camino = new List<DualPathStep>();
                if (anterior is not null && p.Comun >= desde)
                    camino.AddRange(anterior.Take(p.Comun - desde + 1));
                camino.AddRange(propios[p.Id].OrderBy(f => f.Depth).Select(f => new DualPathStep(f.MemberId, (TreeSide)f.Side)));
                caminos[p.Id] = camino;
                anterior = camino;
            }
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
