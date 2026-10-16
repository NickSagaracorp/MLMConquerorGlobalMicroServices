using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

/// <summary>
/// Qué es "el pedido de un alta". Los endpoints del alta son anónimos y se llaman con el id del pedido, así
/// que un 409 como SIGNUP_ALREADY_SETTLED no puede salir para CUALQUIER pedido completado (una renovación,
/// una compra de tokens): serviría para averiguar si un id de pedido existe.
///
/// El pedido de un alta es el primero de su miembro (el que crean SignupAmbassador y SignupMember); los
/// posteriores son renovaciones, compras u otros pedidos.
/// </summary>
public static class SignupEnrollmentOrder
{
    /// <summary>
    /// Estado del pedido <paramref name="orderId"/> si es el de un alta (el primero de su miembro); null si
    /// no existe o es otro tipo de pedido.
    /// </summary>
    public static Task<OrderStatus?> GetStatusAsync(AppDbContext db, string orderId, CancellationToken ct)
        => db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId
                     && !db.Orders.Any(p => p.MemberId == o.MemberId && p.Id != o.Id && p.OrderDate < o.OrderDate))
            .Select(o => (OrderStatus?)o.Status)
            .FirstOrDefaultAsync(ct);
}
