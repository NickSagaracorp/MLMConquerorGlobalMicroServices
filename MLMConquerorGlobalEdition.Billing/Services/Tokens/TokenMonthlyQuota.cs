using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Services.Tokens;

public interface ITokenMonthlyQuota
{
    /// <summary>Tokens que el miembro ya consumió del tope del mes de <paramref name="now"/> (emitidos + en vuelo).</summary>
    Task<int> UsedAsync(string memberId, DateTime now, CancellationToken ct = default);
}

/// <inheritdoc cref="ITokenMonthlyQuota"/>
/// <remarks>
/// <para>
/// UNA COMPRA CUENTA EN UN SOLO MES, el de su <c>TokenPurchase.PeriodKey</c>, desde que reserva hasta
/// que emite. Lo emitido se imputa por el PeriodKey de la compra dueña (TokenTransaction.TokenPurchaseId),
/// no por la fecha de emisión. Contando por CreationDate, una compra en cripto de 50 reservada el 30
/// de septiembre y aún sin confirmar no ocupaba octubre: el miembro compraba otros 50 en octubre, el
/// administrador confirmaba la de septiembre el día 2 y octubre acababa con 100 emitidos.
/// </para>
/// <para>
/// Por eso confirmar una cripto o cerrar una conciliación NO vuelve a comprobar el tope, y menos contra
/// el mes en curso: el cupo se comprobó al reservar, en su mes, y la emisión solo convierte esa reserva
/// en tokens del mismo mes.
/// </para>
/// <para>
/// Lo emitido cuenta por OriginalOwnerMemberId: TransferTokenInstance cambia MemberId, y un tope que
/// se libera regalando tokens no es un tope. Las filas Purchased sin compra (anteriores al Plan F) se
/// siguen imputando por su CreationDate.
/// </para>
/// </remarks>
public sealed class TokenMonthlyQuota : ITokenMonthlyQuota
{
    private readonly AppDbContext _db;

    public TokenMonthlyQuota(AppDbContext db) => _db = db;

    public async Task<int> UsedAsync(string memberId, DateTime now, CancellationToken ct = default)
    {
        var start  = TokenPurchaseRules.MonthStart(now);
        var end    = start.AddMonths(1);
        var period = TokenPurchaseRules.PeriodKey(now);

        var purchased = _db.TokenTransactions.AsNoTracking()
            .Where(t => t.TransactionType == TokenTransactionType.Purchased
                     && t.ReferenceId != null
                     && t.OriginalOwnerMemberId == memberId);

        // IgnoreQueryFilters: una compra borrada lógicamente no devuelve el cupo de lo que ya emitió.
        var issuedByPurchase = await (
            from t in purchased
            join p in _db.TokenPurchases.AsNoTracking().IgnoreQueryFilters() on t.TokenPurchaseId equals p.Id
            where p.PeriodKey == period
            select t.Id).CountAsync(ct);

        var issuedWithoutPurchase = await purchased
            .CountAsync(t => t.TokenPurchaseId == null
                          && t.CreationDate >= start
                          && t.CreationDate < end, ct);

        var reserved = await _db.TokenPurchases.AsNoTracking()
            .Where(p => p.MemberId == memberId
                     && p.PeriodKey == period
                     && (p.Status == TokenPurchaseStatus.Pending
                         || p.Status == TokenPurchaseStatus.AwaitingCryptoConfirmation
                         || p.Status == TokenPurchaseStatus.NeedsReconciliation))
            .SumAsync(p => (int?)p.Quantity, ct) ?? 0;

        return issuedByPurchase + issuedWithoutPurchase + reserved;
    }
}
