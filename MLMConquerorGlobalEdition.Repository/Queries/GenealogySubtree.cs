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
    /// Los nodos VIGENTES (el más reciente de cada miembro) que cuelgan de <paramref name="root"/>, él
    /// incluido. Parte del rango de IX_GenealogyTree_Node (seek, <see cref="Under"/>) y descarta los
    /// obsoletos con el índice (MemberId, CreationDate). Es la forma B: para el equipo entero. Ninguno si
    /// root es null. Es lo que usan Mi equipo, clientes, eventos, puntos y comisiones (revisión I5): con
    /// Under, un miembro con dos nodos bajo el mismo equipo saldría dos veces.
    /// </summary>
    public static IQueryable<GenealogyEntity> CurrentUnder(this IQueryable<GenealogyEntity> tree, HierarchyId? root) =>
        tree.Under(root)
            .Where(g => !tree.Any(o => o.MemberId == g.MemberId
                                    && (o.CreationDate > g.CreationDate
                                        || (o.CreationDate == g.CreationDate && string.Compare(o.Id, g.Id) > 0))));
}
