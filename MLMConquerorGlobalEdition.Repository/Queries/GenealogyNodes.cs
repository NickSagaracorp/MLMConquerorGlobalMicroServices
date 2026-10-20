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
    /// El Node del siguiente hijo de <paramref name="parentMemberId"/>. Una raíz nueva si es null o si el
    /// padre no está en el árbol (la ruta del alta también lo hace raíz: «/{memberId}/», nivel 1).
    /// <para>
    /// Falla cerrado (revisión I4): si el nodo vigente del padre no tiene Node (relleno inicial pendiente), lanza InvalidOperationException. Un hijo sin Node no aparecería en ningún equipo.
    /// </para>
    /// <para>
    /// El padre se busca entre los nodos vivos (filtro de borrado) y es el más reciente, con desempate
    /// por Id (R8 del Plan J). Los hermanos, también entre los borrados: un ordinal no se reutiliza,
    /// porque el índice único no filtra IsDeleted (revisión M1). Dos altas simultáneas bajo el mismo
    /// padre las serializa el alta con GenealogyChildrenGate; el índice único IX_GenealogyTree_Node y
    /// el reintento por IsDuplicateNode quedan como red de seguridad (R5).
    /// </para>
    /// </summary>
    public static async Task<HierarchyId> NextChildAsync(
        AppDbContext db, string? parentMemberId, CancellationToken ct = default)
    {
        var todos = db.GenealogyTree.AsNoTracking().IgnoreQueryFilters();

        if (parentMemberId is not null)
        {
            var nodosDelPadre = await db.GenealogyTree.AsNoTracking()
                .Where(g => g.MemberId == parentMemberId)
                .OrderByDescending(g => g.CreationDate)
                .ThenByDescending(g => g.Id)
                .Select(g => new { g.Node })
                .FirstOrDefaultAsync(ct);

            if (nodosDelPadre is not null)
            {
                var padre = nodosDelPadre.Node
                    ?? throw new GenealogyNodeMissingException(parentMemberId);

                // Los hermanos por ParentMemberId (índice); GetAncestor descarta los que cuelgan de un
                // nodo antiguo del mismo padre.
                var ultimoHijo = await todos
                    .Where(g => g.ParentMemberId == parentMemberId && g.Node != null && g.Node.GetAncestor(1) == padre)
                    .OrderByDescending(g => g.Node)
                    .Select(g => g.Node)
                    .FirstOrDefaultAsync(ct);
                return padre.GetDescendant(ultimoHijo, null);
            }
        }

        // Hijas de la raíz de hierarchyid: índice (NodeLevel, Node), M12.
        var ultimaRaiz = await todos
            .Where(g => g.Node != null && g.Node.GetLevel() == 1)
            .OrderByDescending(g => g.Node)
            .Select(g => g.Node)
            .FirstOrDefaultAsync(ct);
        return HierarchyId.GetRoot().GetDescendant(ultimaRaiz, null);
    }

    /// <summary>True si el guardado falló porque otro alta ya ocupó ese Node (R5).</summary>
    public static bool IsDuplicateNode(DbUpdateException ex)
        => ex.InnerException?.Message is { } m
           && m.Contains(GenealogyConfiguration.NodeIndexName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Plan Q (DQ9): el guardado choco con UX_GenealogyTree_MemberId_Vivo, otro nodo vivo con el mismo MemberId. En el alta
    /// solo pasa si dos altas sacaron el mismo id libre a la vez (el nodo puede insertarse antes que el perfil y chocar
    /// aqui en vez de en AK_MemberProfiles_MemberId): se reintenta con otro id.
    /// </summary>
    public static bool IsDuplicateLiveMember(DbUpdateException ex)
        => ex.InnerException?.Message is { } m
           && m.Contains(GenealogyConfiguration.LiveMemberIndexName, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Plan M (revisión I4): el nodo vigente del patrocinador no tiene Node (relleno pendiente). El alta no
/// cuelga a nadie de él: un nodo sin Node no aparecería en ningún equipo. El alta lo traduce a una
/// respuesta clara (SignupGenealogyErrors.SponsorNotReady), no a un error interno.
/// </summary>
public sealed class GenealogyNodeMissingException(string sponsorMemberId)
    : InvalidOperationException(
        $"El patrocinador {sponsorMemberId} está en el árbol de inscripción pero su nodo vigente no tiene Node " +
        "(relleno pendiente: Scripts/sql/plan-m/02-relleno-nodo-genealogia.sql). No se crea un nodo invisible.")
{
    public string SponsorMemberId { get; } = sponsorMemberId;
}
