using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Lectura del equipo de inscripción por GenealogyTree.Node (Plan M, M16). Sustituye a
/// HierarchyPath.StartsWith(TreePrefix.Of(...)), que recorría la tabla entera. SinPrefijoEnInscripcionTests
/// vigila que nadie vuelva a filtrar el árbol de inscripción por la ruta.
/// </summary>
public static class GenealogySubtree
{
    /// <summary>
    /// Etiqueta (comentario SQL) de las consultas del equipo: RecompileTaggedQueriesInterceptor les añade
    /// OPTION (RECOMPILE), porque el plan bueno depende del tamaño del equipo (Plan M).
    /// </summary>
    public const string RecompileTag = "GenealogySubtree.Under: recompilar por tamano de equipo";

    /// <summary>
    /// El nodo vigente del miembro: el más reciente (R8 del Plan J), con desempate por Id (revisión M3).
    /// Null si no está en el árbol. Es el único sitio que elige el nodo de un miembro (revisión M2): un
    /// FirstOrDefault sin orden podía devolver el obsoleto.
    /// </summary>
    public static Task<GenealogyEntity?> CurrentOfAsync(
        this IQueryable<GenealogyEntity> tree, string memberId, CancellationToken ct = default) =>
        tree.Where(g => g.MemberId == memberId)
            .OrderByDescending(g => g.CreationDate)
            .ThenByDescending(g => g.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>El Node del nodo vigente del miembro (<see cref="CurrentOfAsync"/>). Null si no tiene.</summary>
    public static Task<HierarchyId?> NodeOfAsync(
        this IQueryable<GenealogyEntity> tree, string memberId, CancellationToken ct = default) =>
        tree.Where(g => g.MemberId == memberId)
            .OrderByDescending(g => g.CreationDate)
            .ThenByDescending(g => g.Id)
            .Select(g => g.Node)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Plan Q (revision del bloque 4): el Node del nodo vigente del miembro (<see cref="CurrentOfAsync"/>) contando lo que
    /// <paramref name="context"/> va a guardar: un nodo nuevo seguido gana (es el mas reciente); el vigente de la base, con
    /// sus valores seguidos si se esta cambiando; null si el vigente no tiene Node o se borra. Es la definicion unica de
    /// «tiene nodo de inscripcion» al encolar (MembershipPointsDeltas, QualifiedTeamDeltas) y la misma que aplica
    /// ApplyMemberStatisticDeltasJob (el TOP 1 vivo por CreationDate e Id, y despues Node no nulo): un vigente sin Node no
    /// tiene linea, aunque un nodo vivo mas viejo la tenga.
    /// </summary>
    public static async Task<HierarchyId?> CurrentNodeAsync(this DbContext context, string memberId, CancellationToken ct = default)
    {
        var nuevo = context.ChangeTracker.Entries<GenealogyEntity>()
            .Where(e => e.State == EntityState.Added && e.Entity.MemberId == memberId && !e.Entity.IsDeleted)
            .Select(e => e.Entity)
            .OrderByDescending(g => g.CreationDate).ThenByDescending(g => g.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (nuevo is not null) return nuevo.Node;

        var vigente = await context.Set<GenealogyEntity>().AsNoTracking().Where(g => g.MemberId == memberId)
            .OrderByDescending(g => g.CreationDate).ThenByDescending(g => g.Id)
            .Select(g => new { g.Id, g.Node }).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (vigente is null) return null;
        var seguido = context.ChangeTracker.Entries<GenealogyEntity>().FirstOrDefault(e => e.Entity.Id == vigente.Id);
        if (seguido is null) return vigente.Node;
        return seguido.State == EntityState.Deleted || seguido.Entity.IsDeleted ? null : seguido.Entity.Node;
    }

    /// <summary>
    /// Plan Q (revision del bloque 4): el primer patrocinador de la cadena de <paramref name="memberId"/> (sin el) cuyo nodo
    /// vigente tiene Node (<see cref="CurrentNodeAsync"/>), con guarda de ciclos; null si nadie lo tiene. Es donde va la
    /// linea de quien aun no tiene nodo: quedara en el equipo de ese patrocinador.
    /// </summary>
    public static async Task<string?> FirstSponsorWithNodeAsync(this DbContext context, string memberId, CancellationToken ct = default)
    {
        var vistos = new HashSet<string>(StringComparer.Ordinal) { memberId };
        var actual = memberId;
        while (true)
        {
            var id = actual;
            var patrocinador = context.ChangeTracker.Entries<MemberProfile>()
                                   .Where(e => e.Entity.MemberId == id).Select(e => e.Entity.SponsorMemberId).FirstOrDefault()
                               ?? await context.Set<MemberProfile>().AsNoTracking().IgnoreQueryFilters().Where(m => m.MemberId == id)
                                   .Select(m => m.SponsorMemberId).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(patrocinador) || !vistos.Add(patrocinador)) return null;
            if (await context.CurrentNodeAsync(patrocinador, ct).ConfigureAwait(false) is not null) return patrocinador;
            actual = patrocinador;
        }
    }

    /// <summary>
    /// Todos los nodos (vigentes u obsoletos) que cuelgan de <paramref name="root"/>, él incluido; ninguno
    /// si <paramref name="root"/> es null (quien no tiene Node no tiene equipo). Es el rango del índice en
    /// profundidad: los descendientes de root van, en profundidad, entre root y su hermano siguiente.
    /// <para>
    /// No se escribe IsDescendantOf: EF lo manda como IsDescendantOf(@p) = CAST(1 AS bit), que con
    /// parámetro no se convierte en seek, y con él en el filtro SQL Server estima una fila para cualquier
    /// subárbol y elige bucles con búsquedas por clave. Medido en el Plan M con el equipo de 119.400
    /// personas: 9 s con IsDescendantOf, 46 ms con el rango.
    /// </para>
    /// <para>
    /// El rango es exacto porque los ordinales son enteros: solo los crean GenealogyNodes.NextChildAsync
    /// (el siguiente al último hermano) y el relleno (02-relleno-nodo-genealogia.sql), y ninguno pone un
    /// nodo ENTRE dos hermanos (el «/1.1/» que daría GetDescendant(a, b)). Lo vigila
    /// SqlServerNodoDeGenealogiaTests. Cualquier consulta del equipo sobre GenealogyTree pasa por aquí.
    /// </para>
    /// </summary>
    public static IQueryable<GenealogyEntity> Under(this IQueryable<GenealogyEntity> tree, HierarchyId? root)
    {
        if (root is null)
            return tree.Where(g => false);

        tree = tree.TagWith(RecompileTag);
        var parent = root.GetAncestor(1);
        if (parent is null)                      // la raíz de hierarchyid: el árbol entero
            return tree.Where(g => g.Node != null);

        var next = parent.GetDescendant(root, null);
        return tree.Where(g => g.Node >= root && g.Node < next);
    }

    /// <summary>
    /// Plan Q, revision del bloque 5 (I1): cuantos nodos vivos cuelgan de <paramref name="root"/>, el incluido. Es lo que
    /// listan Mi equipo (menos el que mira) y los eventos del equipo (un nodo vivo por miembro, DQ9), tambien los
    /// pendientes; MemberStatistics.EnrollmentTeamSize solo cuenta activados y no sirve de total. Cuenta el rango por el
    /// indice estrecho IX_GenealogyTree_Node_Vivo, con la pista: por si solo el optimizador elegia IX_GenealogyTree_Node
    /// (1.910 paginas frente a 1.640 con 119.000 en local). 0 si root es null.
    /// </summary>
    public static Task<int> CountUnderAsync(this DbContext db, HierarchyId? root, CancellationToken ct = default)
    {
        if (root is null) return Task.FromResult(0);
        if (!db.Database.IsSqlServer()) return db.Set<GenealogyEntity>().Under(root).CountAsync(ct);
        var parent = root.GetAncestor(1);
        if (parent is null)                      // la raiz de hierarchyid: el arbol entero
            return db.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS [Value] FROM GenealogyTree WITH (INDEX(IX_GenealogyTree_Node_Vivo)) " +
                    "WHERE IsDeleted = 0 AND Node IS NOT NULL")
                .SingleAsync(ct);
        return db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS [Value] FROM GenealogyTree WITH (INDEX(IX_GenealogyTree_Node_Vivo)) " +
                "WHERE IsDeleted = 0 AND Node >= CAST(@desde AS hierarchyid) AND Node < CAST(@hasta AS hierarchyid)",
                new SqlParameter("@desde", SqlDbType.NVarChar, 4000) { Value = root.ToString() },
                new SqlParameter("@hasta", SqlDbType.NVarChar, 4000) { Value = parent.GetDescendant(root, null).ToString() })
            .SingleAsync(ct);
    }

    /// <summary>
    /// Plan Q (Tarea 26), revision del bloque 5 (I4): una pagina del equipo de <paramref name="root"/> en el orden del arbol
    /// (Node), tomada del recorrido de IX_GenealogyTree_Node (cubriente) ANTES de unir con nada; quien llama une esas pocas
    /// filas con sus perfiles y vuelve a ordenarlas por Node. Con el OFFSET sobre la union, el optimizador unia el rango
    /// entero con los perfiles y lo ordenaba. <paramref name="excludeMemberId"/>: el que mira, si no cuenta como fila.
    /// </summary>
    public static IQueryable<GenealogyEntity> PageUnder(
        this IQueryable<GenealogyEntity> tree, HierarchyId? root, int skip, int take, string? excludeMemberId = null)
    {
        var equipo = tree.Under(root);
        if (excludeMemberId is not null) equipo = equipo.Where(g => g.MemberId != excludeMemberId);
        return equipo.OrderBy(g => g.Node).Skip(skip).Take(take);
    }

    /// <summary>
    /// Plan Q (DQ8, revision del bloque 5 I2): los nodos vivos bajo <paramref name="root"/> (el incluido) cuyo miembro casa
    /// con <paramref name="term"/> por el comienzo del nombre completo o del id. Es la forma de los equipos grandes de
    /// DualSubtree.SearchUnder: de los perfiles que casan (MemberNameSearch.MatchingIdsSql, con tope por rama) a su nodo vivo
    /// (UX_GenealogyTree_MemberId_Vivo), comprobando el rango de Node. Mi equipo solo la usa por encima de
    /// TeamGridLimits.FreeMaxRows; por debajo busca por subcadena sobre el equipo. Ninguno si root es null. Sin recompilar:
    /// el plan lo fijan la pista LOOP y los topes.
    /// </summary>
    public static IQueryable<GenealogyEntity> SearchUnder(
        this DbContext db, HierarchyId? root, string term, int candidatesPerBranch)
    {
        var tree = db.Set<GenealogyEntity>();
        if (root is null) return tree.Where(g => false);
        if (!db.Database.IsSqlServer())
        {
            var ids = MemberNameSearch.ApplyInMemory(db.Set<MemberProfile>(), term).Select(m => m.MemberId);
            return tree.Under(root).Where(g => ids.Contains(g.MemberId));
        }

        var (candidatos, parametros) = MemberNameSearch.MatchingIdsSql(term, candidatesPerBranch);
        var parent = root.GetAncestor(1);
        // El mismo rango que Under; los Node van como texto y se convierten una vez (sigue siendo un rango del indice).
        var rango = parent is null
            ? "g.Node IS NOT NULL"
            : "g.Node >= CAST(@desde AS hierarchyid) AND g.Node < CAST(@hasta AS hierarchyid)";
        var todos = parametros.ToList<object>();
        if (parent is not null)
        {
            todos.Add(new SqlParameter("@desde", SqlDbType.NVarChar, 4000) { Value = root.ToString() });
            todos.Add(new SqlParameter("@hasta", SqlDbType.NVarChar, 4000) { Value = parent.GetDescendant(root, null).ToString() });
        }
        return tree
            // LOOP: de los candidatos (como mucho el tope por rama) a su nodo vivo, por el indice unico de MemberId.
            .FromSqlRaw($@"SELECT g.* FROM (
    {candidatos}) m
INNER LOOP JOIN GenealogyTree g ON g.MemberId = m.MemberId AND g.IsDeleted = 0
WHERE {rango}", todos.ToArray());
    }

    /// <summary>
    /// Los nodos vigentes que cuelgan de <paramref name="root"/>, el incluido. Plan Q: con UX_GenealogyTree_MemberId_Vivo
    /// cada miembro tiene un solo nodo vivo, asi que el vigente es el unico y basta el rango (<see cref="Under"/>). Se
    /// mantiene el nombre: es lo que usan Mi equipo, clientes, eventos, puntos y comisiones.
    /// </summary>
    public static IQueryable<GenealogyEntity> CurrentUnder(this IQueryable<GenealogyEntity> tree, HierarchyId? root) =>
        tree.Under(root);
}
