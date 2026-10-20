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

    /// <summary>
    /// Plan Q (DQ8): los nodos bajo <paramref name="node"/> (el no) cuyo miembro casa con <paramref name="term"/> por el
    /// comienzo del nombre completo o del id (MemberNameSearch). Parte de los perfiles que casan, unos pocos, y comprueba el
    /// rango de la clave; asi el optimizador, recompilado por la etiqueta, busca primero por el indice de nombre.
    /// </summary>
    public static IQueryable<DualTeamEntity> SearchUnder(AppDbContext db, DualTeamEntity node, string term)
    {
        if (!db.Database.IsSqlServer())
        {
            var ids = MemberNameSearch.Apply(db, db.MemberProfiles, term).Select(m => m.MemberId);
            return Under(db, node).Where(d => ids.Contains(d.MemberId));
        }
        var (lo, hi) = DualBinaryPath.SubtreeBounds(Key(node));
        return db.DualTeamTree
            // Una rama por indice (UNION): con los tres LIKE en un OR el optimizador recorria MemberProfiles entero
            // (19.500 lecturas, de 260 a 580 ms en local para 1.768 coincidencias); asi son tres Index Seek. LOOP: de los
            // perfiles que casan a su nodo por IX_DualTeamTree_MemberId; sin la pista, con la carga de la maquina, a veces
            // elegia recorrer el rango entero del equipo y unir por hash (mas de 1 s para contar 1.766).
            .FromSqlRaw(@"SELECT d.* FROM (
    SELECT MemberId FROM MemberProfiles WHERE SearchName LIKE @t AND IsDeleted = 0
    UNION SELECT MemberId FROM MemberProfiles WHERE SearchNameReversed LIKE @t AND IsDeleted = 0
    UNION SELECT MemberId FROM MemberProfiles WHERE MemberId LIKE @t AND IsDeleted = 0) m
INNER LOOP JOIN DualTeamTree d ON d.MemberId = m.MemberId AND d.IsDeleted = 0
WHERE d.BinaryPath > @lo AND d.BinaryPath < @hi AND d.MemberId <> @self",
                new SqlParameter("@t", SqlDbType.NVarChar, 210) { Value = MemberNameSearch.Pattern(term) },
                Param("@lo", lo), Param("@hi", hi),
                new SqlParameter("@self", SqlDbType.NVarChar, 450) { Value = node.MemberId })
            .TagWith(RecompileTag);
    }

    /// <summary>
    /// Plan Q (Tarea 28): una pagina de los descendientes de <paramref name="node"/> (el no) en el orden de la clave, la
    /// estructura del arbol. La pagina se toma del indice cubriente UX_DualTeamTree_BinaryPath (solo MemberId) y despues se
    /// leen esos pocos nodos; con el OFFSET sobre el equipo unido a los perfiles se ordenaba el rango entero (1,9 s la
    /// pagina 1.000 con 119.000 en local). Quien llama vuelve a ordenar las filas de la pagina por la clave. En InMemory,
    /// por profundidad e id.
    /// </summary>
    public static IQueryable<DualTeamEntity> PageUnder(AppDbContext db, DualTeamEntity node, int skip, int take)
    {
        if (!db.Database.IsSqlServer())
            return Under(db, node).OrderBy(d => d.Depth).ThenBy(d => d.MemberId).Skip(skip).Take(take);
        var (lo, hi) = DualBinaryPath.SubtreeBounds(Key(node));
        return db.DualTeamTree
            .FromSqlRaw(@"SELECT d.* FROM (
    SELECT MemberId FROM DualTeamTree WITH (INDEX(UX_DualTeamTree_BinaryPath))
    WHERE BinaryPath > @lo AND BinaryPath < @hi AND IsDeleted = 0 AND MemberId <> @self
    ORDER BY BinaryPath OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY) p
JOIN DualTeamTree d ON d.MemberId = p.MemberId AND d.IsDeleted = 0",
                Param("@lo", lo), Param("@hi", hi),
                new SqlParameter("@self", SqlDbType.NVarChar, 450) { Value = node.MemberId },
                new SqlParameter("@skip", SqlDbType.Int) { Value = skip },
                new SqlParameter("@take", SqlDbType.Int) { Value = take })
            .TagWith(RecompileTag);
    }

    /// <summary>
    /// Plan Q: solo los MemberId de los descendientes de <paramref name="node"/>, leidos por el indice cubriente
    /// UX_DualTeamTree_BinaryPath (con la pista: con SELECT * el optimizador elegia a veces una interseccion paralela
    /// de tres indices, de 400 a 1.660 ms con 119.000; asi, unos 200 ms sin carga). Para contar o unir con otra tabla.
    /// </summary>
    public static IQueryable<string> MemberIdsUnder(AppDbContext db, DualTeamEntity node)
    {
        if (!db.Database.IsSqlServer())
            return Under(db, node).Select(d => d.MemberId);
        var (lo, hi) = DualBinaryPath.SubtreeBounds(Key(node));
        return db.Database
            .SqlQueryRaw<DualMemberIdRow>(
                "SELECT MemberId FROM DualTeamTree WITH (INDEX(UX_DualTeamTree_BinaryPath)) " +
                "WHERE BinaryPath > @lo AND BinaryPath < @hi AND IsDeleted = 0 AND MemberId <> @self",
                Param("@lo", lo), Param("@hi", hi),
                new SqlParameter("@self", SqlDbType.NVarChar, 450) { Value = node.MemberId })
            .TagWith(RecompileTag)
            .Select(r => r.MemberId);
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

    /// <summary>Fila de <see cref="MemberIdsUnder"/>: EF no traduce un join contra SqlQueryRaw&lt;string&gt;.</summary>
    public sealed class DualMemberIdRow
    {
        public string MemberId { get; set; } = string.Empty;
    }

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
