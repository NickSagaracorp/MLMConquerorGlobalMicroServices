using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Lectura del equipo de inscripción por GenealogyTree.Node (Plan M, M16). Sustituye a
/// HierarchyPath.StartsWith(TreePrefix.Of(...)), que recorría la tabla entera. SinPrefijoEnInscripcionTests
/// vigila que nadie vuelva a filtrar el árbol de inscripción por la ruta.
/// </summary>
public static class GenealogySubtree
{
    /// <summary>El Node vigente del miembro: el de su nodo más reciente (R8 del Plan J). Null si no tiene.</summary>
    public static Task<HierarchyId?> NodeOfAsync(
        this IQueryable<GenealogyEntity> tree, string memberId, CancellationToken ct = default) =>
        tree.Where(g => g.MemberId == memberId)
            .OrderByDescending(g => g.CreationDate)
            .Select(g => g.Node)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Los nodos VIGENTES (el más reciente de cada miembro) que cuelgan de <paramref name="root"/>, él
    /// incluido. Parte del rango de IX_GenealogyTree_Node (seek) y descarta los obsoletos con el índice
    /// (MemberId, CreationDate). Es la forma B: para el equipo entero.
    /// </summary>
    public static IQueryable<GenealogyEntity> CurrentUnder(this IQueryable<GenealogyEntity> tree, HierarchyId root) =>
        tree.Where(g => g.Node!.IsDescendantOf(root)
                     && !tree.Any(o => o.MemberId == g.MemberId && o.CreationDate > g.CreationDate));
}
