using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Plan Q (DQ10): el unico sitio que calcula y guarda MemberProfiles.SignupMembershipLevelId. Regla N2 del Plan J: el
/// primer pedido del miembro con productos de membresia (por OrderDate y CreationDate), su nivel mas alto (la regla de
/// <see cref="OrderMembershipLevelQueries"/>); no se filtra por estado del pedido. Lo llaman la eleccion de productos del
/// alta y la activacion; 02-nivel-de-alta.sql rellena el pasado.
/// </summary>
public static class SignupMembershipLevel
{
    public static async Task<int?> ComputeAsync(AppDbContext db, string memberId, CancellationToken ct = default)
    {
        var porPedido = await (
            from o in db.Orders.AsNoTracking()
            where o.MemberId == memberId
            join od in db.OrderDetails.AsNoTracking() on o.Id equals od.OrderId
            join p in db.Products.AsNoTracking() on od.ProductId equals p.Id
            where p.MembershipLevelId != null
            group p.MembershipLevelId!.Value by new { o.Id, o.OrderDate, o.CreationDate } into g
            select new { g.Key.OrderDate, g.Key.CreationDate, Nivel = g.Max() }).ToListAsync(ct);
        return porPedido.OrderBy(x => x.OrderDate).ThenBy(x => x.CreationDate).Select(x => (int?)x.Nivel).FirstOrDefault();
    }

    /// <summary>Recalcula y asigna en el perfil seguido (quien llama guarda).</summary>
    public static async Task RefreshAsync(AppDbContext db, Domain.Entities.Member.MemberProfile member, CancellationToken ct = default) =>
        member.SignupMembershipLevelId = await ComputeAsync(db, member.MemberId, ct);
}
