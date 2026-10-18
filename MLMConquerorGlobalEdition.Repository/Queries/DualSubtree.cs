using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Plan P: el equipo binario de un nodo es el rango (Lo, Hi) de UX_DualTeamTree_BinaryPath (DualBinaryPath):
/// un Index Seek, sin recorrer la tabla ni bajar nivel a nivel. Sustituye a la CTE con tope de 20.000 del
/// Plan M y a los filtros por prefijo de HierarchyPath (que ademas fallan con el error 8152 cuando la ruta pasa
/// de 4.000 caracteres: tras la reparacion, casi todos los nodos). Las consultas llevan <see cref="RecompileTag"/>:
/// el tamano del rango va de 1 a cientos de miles, y RecompileTaggedQueriesInterceptor les anade OPTION (RECOMPILE).
/// Fuera de SQL Server (InMemory, pruebas de logica) el mismo conjunto sale de bajar por ParentMemberId.
/// </summary>
public static class DualSubtree
{
    public const string RecompileTag = "DualSubtree.Range: recompilar por tamano de equipo";

    /// <summary>Descendientes de <paramref name="node"/> (el no).</summary>
    public static IQueryable<DualTeamEntity> Under(AppDbContext db, DualTeamEntity node)
    {
        if (!db.Database.IsSqlServer())
        {
            var ids = DescendantIdsInMemory(db, node.MemberId, side: null);
            return db.DualTeamTree.Where(d => ids.Contains(d.MemberId));
        }
        var (lo, hi) = DualBinaryPath.SubtreeBounds(Key(node));
        return Range(db, lo, hi).Where(d => d.MemberId != node.MemberId);
    }

    /// <summary>Los nodos de una pierna de <paramref name="node"/> (el subarbol de su hijo de ese lado).</summary>
    public static IQueryable<DualTeamEntity> Leg(AppDbContext db, DualTeamEntity node, TreeSide side)
    {
        if (!db.Database.IsSqlServer())
        {
            var ids = DescendantIdsInMemory(db, node.MemberId, side);
            return db.DualTeamTree.Where(d => ids.Contains(d.MemberId));
        }
        var (lo, hi) = DualBinaryPath.LegBounds(Key(node), side);
        return Range(db, lo, hi);
    }

    /// <summary>La pierna de <paramref name="node"/> en la que esta <paramref name="descendant"/> (inorden: izquierda antes).</summary>
    public static TreeSide LegOf(DualTeamEntity node, byte[] descendant) =>
        DualBinaryPath.SqlCompare(descendant, Key(node)) < 0 ? TreeSide.Left : TreeSide.Right;

    private static IQueryable<DualTeamEntity> Range(AppDbContext db, byte[] lo, byte[] hi) =>
        db.DualTeamTree
            .FromSqlRaw("SELECT * FROM DualTeamTree WHERE BinaryPath > @lo AND BinaryPath < @hi AND IsDeleted = 0",
                Param("@lo", lo), Param("@hi", hi))
            .TagWith(RecompileTag);

    private static SqlParameter Param(string name, byte[] value) =>
        new(name, SqlDbType.VarBinary, DualBinaryPath.MaxBytes) { Value = value };

    private static byte[] Key(DualTeamEntity n) =>
        n.BinaryPath ?? throw new InvalidOperationException(
            $"{n.MemberId} has no BinaryPath (backfill pending: Scripts/sql/plan-p/03-relleno-clave-binario.sql).");

    private static List<string> DescendantIdsInMemory(AppDbContext db, string rootId, TreeSide? side)
    {
        var nivel = db.DualTeamTree.Where(d => d.ParentMemberId == rootId && (side == null || d.Side == side))
            .Select(d => d.MemberId).ToList();
        var todos = new List<string>();
        while (nivel.Count > 0)
        {
            todos.AddRange(nivel);
            var actual = nivel;
            nivel = db.DualTeamTree.Where(d => d.ParentMemberId != null && actual.Contains(d.ParentMemberId))
                .Select(d => d.MemberId).ToList().Except(todos).ToList();
        }
        return todos;
    }
}
