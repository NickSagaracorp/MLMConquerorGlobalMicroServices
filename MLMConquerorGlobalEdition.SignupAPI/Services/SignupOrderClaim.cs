using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.SignupAPI.Services;

/// <summary>
/// La transición del pedido de alta a Completed, atómica en la base. Es lo que garantiza que un pedido
/// se activa UNA vez aunque el dinero llegue por dos sitios a la vez (cripto y Entrax, o dos QR de
/// Entrax del mismo pedido): Orders no tiene token de concurrencia (su RowVersion es varbinary(max) sin
/// IsRowVersion), así que dos caminos que leyeran Processing guardarían los dos.
/// </summary>
public interface ISignupOrderClaim
{
    /// <summary>
    /// Pasa el pedido de Pending o Processing a Completed. Devuelve false si otro camino ya lo había
    /// sacado de ahí: quien recibe false no activa nada. En SQL Server ejecuta el UPDATE en el acto,
    /// así que tiene que ir dentro de la transacción de quien activa (si no, lanza).
    /// </summary>
    Task<bool> TryCompleteAsync(Orders order, DateTime now, string actor, CancellationToken ct);

    /// <summary>
    /// Igual, pero SOLO desde Pending: el camino inmediato (tarjeta, token, código de descuento). Un
    /// pedido en Processing está esperando otro cobro (un QR de Entrax, una cripto) y no lo completa
    /// la tarjeta. También dentro de la transacción de quien activa.
    /// </summary>
    Task<bool> TryCompleteFromPendingAsync(Orders order, DateTime now, string actor, CancellationToken ct);
}

/// <inheritdoc />
public sealed class SignupOrderClaim : ISignupOrderClaim
{
    /// <summary>El pedido ya se activó por otro camino: el dinero que llega ahora es un doble cobro.</summary>
    public const string AlreadySettledCode = "ORDER_ALREADY_SETTLED";

    private readonly AppDbContext _db;

    public SignupOrderClaim(AppDbContext db) => _db = db;

    public Task<bool> TryCompleteAsync(Orders order, DateTime now, string actor, CancellationToken ct)
        => TryMoveAsync(order, [OrderStatus.Pending, OrderStatus.Processing], OrderStatus.Completed, now, actor,
                        requireTransaction: true, ct);

    public Task<bool> TryCompleteFromPendingAsync(Orders order, DateTime now, string actor, CancellationToken ct)
        => TryMoveAsync(order, [OrderStatus.Pending], OrderStatus.Completed, now, actor, requireTransaction: true, ct);

    private async Task<bool> TryMoveAsync(
        Orders order, OrderStatus[] from, OrderStatus to, DateTime now, string actor, bool requireTransaction,
        CancellationToken ct)
    {
        if (!_db.Database.IsRelational())
        {
            // EF InMemory (pruebas) no tiene UPDATE condicional: se decide con lo leído.
            return from.Contains(order.Status);
        }

        // Fallo cerrado: fuera de una transacción el UPDATE quedaría confirmado aunque la activación
        // fallara después, y el pedido quedaría Completed sin miembro activo ni comisiones.
        if (requireTransaction && _db.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "SignupOrderClaim must run inside the activation's database transaction.");

        // El segundo que llegue espera al bloqueo de la fila y, cuando el primero confirma, ya no la
        // encuentra en el estado de partida: cero filas.
        var affected = await _db.Orders
            .Where(o => o.Id == order.Id && from.Contains(o.Status))
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, to)
                .SetProperty(o => o.LastUpdateDate, now)
                .SetProperty(o => o.LastUpdateBy, actor), ct);

        if (affected != 1)
            return false;

        // La entidad seguida refleja lo que ya está en la base, sin quedar marcada como modificada.
        var entry = _db.Entry(order);
        if (entry.State != EntityState.Detached)
        {
            entry.Property(o => o.Status).CurrentValue  = to;
            entry.Property(o => o.Status).OriginalValue = to;
        }
        return true;
    }
}
