using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Configurations;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Escritura de GenealogyTree.Node (Plan M, M13). Único sitio que decide el Node de un nodo nuevo.
/// Mover un subárbol (cambio de patrocinador, hoy inexistente) NO se hace nodo a nodo: un único UPDATE
/// con Node.GetReparentedValue(@viejo, @nuevo) sobre todo el subárbol, en una transacción con
/// sp_getapplock sobre los dos patrocinadores y con el ordinal nuevo reservado aquí (R3).
/// </summary>
public static class GenealogyNodes
{
    /// <summary>
    /// El Node del siguiente hijo de <paramref name="parentMemberId"/> (null = una raíz nueva). Devuelve
    /// null si el padre todavía no tiene Node (relleno inicial pendiente): el relleno lo pondrá.
    /// Dos altas simultáneas bajo el mismo padre obtienen el mismo valor; el índice único
    /// IX_GenealogyTree_Node rechaza a la segunda y el alta reintenta (IsDuplicateNode).
    /// </summary>
    public static async Task<HierarchyId?> NextChildAsync(
        AppDbContext db, string? parentMemberId, CancellationToken ct = default)
    {
        // Con los borrados lógicos: un ordinal no se reutiliza (el índice único no filtra IsDeleted).
        var arbol = db.GenealogyTree.AsNoTracking().IgnoreQueryFilters();

        if (parentMemberId is null)
        {
            // Hijas de la raíz de hierarchyid: índice (NodeLevel, Node), M12.
            var ultimaRaiz = await arbol
                .Where(g => g.Node != null && g.Node.GetLevel() == 1)
                .OrderByDescending(g => g.Node)
                .Select(g => g.Node)
                .FirstOrDefaultAsync(ct);
            return HierarchyId.GetRoot().GetDescendant(ultimaRaiz, null);
        }

        // El nodo más reciente del padre (R8 del Plan J).
        var padre = await arbol
            .Where(g => g.MemberId == parentMemberId && g.Node != null)
            .OrderByDescending(g => g.CreationDate)
            .Select(g => g.Node)
            .FirstOrDefaultAsync(ct);
        if (padre is null)
            return null;

        // Los hermanos por ParentMemberId (índice); GetAncestor descarta los que cuelgan de un nodo
        // antiguo del mismo padre.
        var ultimoHijo = await arbol
            .Where(g => g.ParentMemberId == parentMemberId && g.Node != null && g.Node.GetAncestor(1) == padre)
            .OrderByDescending(g => g.Node)
            .Select(g => g.Node)
            .FirstOrDefaultAsync(ct);

        return padre.GetDescendant(ultimoHijo, null);
    }

    /// <summary>True si el guardado falló porque otro alta ya ocupó ese Node (R5).</summary>
    public static bool IsDuplicateNode(DbUpdateException ex)
        => ex.InnerException?.Message is { } m
           && m.Contains(GenealogyConfiguration.NodeIndexName, StringComparison.OrdinalIgnoreCase);
}
