using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Derives the membership level of an order from its products.
///
/// Por diseño un pedido puede llevar hasta dos productos con MembershipLevelId: el
/// producto base (Lifestyle Ambassador, nivel 1, siempre presente) y, opcionalmente,
/// un producto de membresía superior (VIP/Elite/Turbo, niveles 2-4). Esto es correcto
/// y no hay que "arreglarlo" en el catálogo — lo único que hay que fijar es CÓMO se
/// elige un único nivel cuando un pedido trae más de uno: siempre el más alto de los
/// productos del pedido, nunca "el primero que devuelva el motor".
///
/// Antes de esta clase, cinco sitios repetían la misma consulta OrderDetails⋈Products
/// y dos de ellos la resolvían con <c>FirstOrDefaultAsync</c> sin ORDER BY. Sin un
/// orden explícito, SQL Server puede devolver cualquier fila cuando el pedido tiene
/// más de un producto con nivel — el resultado no es determinista y cambiaba entre
/// ejecuciones idénticas, dejando bonos de patrocinador sin pagar al azar. Los otros
/// tres sitios ya usaban <c>Max()</c>/<c>g.Max()</c> correctamente. Esta clase es el
/// único lugar que implementa la regla ("el nivel más alto") para que los cinco la
/// compartan y no puedan volver a divergir.
///
/// Vive en Repository (no en SharedKernel) porque necesita EF Core y AppDbContext —
/// SharedKernel no puede depender de ellos (ver el comentario de invariante en su
/// .csproj) — y tanto SignupAPI como CommissionEngine ya referencian este proyecto.
/// </summary>
public static class OrderMembershipLevelQueries
{
    /// <summary>
    /// Nivel de membresía más alto entre los productos de <paramref name="orderId"/>.
    /// Devuelve 0 si el pedido no tiene ningún producto con MembershipLevelId (p. ej.
    /// pedidos de productos que no son de membresía).
    /// </summary>
    public static async Task<int> GetHighestMembershipLevelIdAsync(
        this AppDbContext db, string orderId, CancellationToken ct = default)
    {
        var levelIds = await (
            from od in db.OrderDetails.AsNoTracking()
            join p in db.Products.AsNoTracking() on od.ProductId equals p.Id
            where od.OrderId == orderId && p.MembershipLevelId.HasValue
            select p.MembershipLevelId!.Value
        ).ToListAsync(ct);

        return levelIds.Count == 0 ? 0 : levelIds.Max();
    }

    /// <summary>
    /// Nivel de membresía más alto por pedido, para un lote de pedidos a la vez —
    /// usado por los jobs de barrido que procesan muchos pedidos por corrida.
    /// Cuando <paramref name="eligibleLevelIds"/> no es null, sólo se consideran
    /// productos cuyo nivel esté en ese conjunto (y un pedido cuyo único nivel
    /// caiga fuera del filtro simplemente no aparece en el resultado — igual que
    /// antes). Cuando es null, se considera cualquier nivel presente en el pedido.
    /// </summary>
    public static Task<Dictionary<string, int>> GetHighestMembershipLevelIdsByOrderAsync(
        this AppDbContext db, int[]? eligibleLevelIds = null, CancellationToken ct = default)
    {
        var query =
            from od in db.OrderDetails.AsNoTracking()
            join p in db.Products.AsNoTracking() on od.ProductId equals p.Id
            where p.MembershipLevelId.HasValue
               && (eligibleLevelIds == null || eligibleLevelIds.Contains(p.MembershipLevelId!.Value))
            group p.MembershipLevelId!.Value by od.OrderId into g
            select new { OrderId = g.Key, LevelId = g.Max() };

        return query.ToDictionaryAsync(x => x.OrderId, x => x.LevelId, ct);
    }
}
