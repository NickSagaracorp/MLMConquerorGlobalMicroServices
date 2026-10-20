using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Plan Q (DQ10): el unico sitio que calcula y guarda MemberProfiles.SignupMembershipLevelId. Regla N2 del Plan J: el
/// primer pedido del miembro con productos de membresia (por OrderDate, CreationDate e Id), su nivel mas alto (la regla de
/// <see cref="OrderMembershipLevelQueries"/>); no se filtra por estado del pedido. Lo llaman la eleccion de productos del
/// alta y la activacion; 02-nivel-de-alta.sql rellena el pasado.
/// </summary>
public static class SignupMembershipLevel
{
    public static Task<int?> ComputeAsync(AppDbContext db, string memberId, CancellationToken ct = default) =>
        // Revision del bloque 5 (I6): el orden va en SQL y termina en o.Id, como 02-nivel-de-alta.sql: dos pedidos con la
        // misma fecha de pedido y de creacion se desempatan igual en los dos sitios (la misma intercalacion de la base).
        (from o in db.Orders.AsNoTracking()
         where o.MemberId == memberId
         join od in db.OrderDetails.AsNoTracking() on o.Id equals od.OrderId
         join p in db.Products.AsNoTracking() on od.ProductId equals p.Id
         where p.MembershipLevelId != null
         group p.MembershipLevelId!.Value by new { o.Id, o.OrderDate, o.CreationDate } into g
         orderby g.Key.OrderDate, g.Key.CreationDate, g.Key.Id
         select (int?)g.Max()).FirstOrDefaultAsync(ct);

    /// <summary>Recalcula y asigna en el perfil seguido (quien llama guarda).</summary>
    public static async Task RefreshAsync(AppDbContext db, Domain.Entities.Member.MemberProfile member, CancellationToken ct = default) =>
        member.SignupMembershipLevelId = await ComputeAsync(db, member.MemberId, ct);
}
