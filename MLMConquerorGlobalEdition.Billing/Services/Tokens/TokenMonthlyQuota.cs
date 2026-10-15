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
/// Lo emitido cuenta por OriginalOwnerMemberId: TransferTokenInstance cambia MemberId, y un tope que
/// se libera regalando tokens no es un tope. Una compra en cripto pedida en septiembre y confirmada en
/// octubre cuenta en octubre (sus tokens nacen entonces); como reserva solo ocupó septiembre.
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

        var issued = await _db.TokenTransactions.AsNoTracking()
            .CountAsync(t => t.TransactionType == TokenTransactionType.Purchased
                          && t.ReferenceId != null
                          && t.OriginalOwnerMemberId == memberId
                          && t.CreationDate >= start
                          && t.CreationDate < end, ct);

        var reserved = await _db.TokenPurchases.AsNoTracking()
            .Where(p => p.MemberId == memberId
                     && p.PeriodKey == period
                     && (p.Status == TokenPurchaseStatus.Pending
                         || p.Status == TokenPurchaseStatus.AwaitingCryptoConfirmation
                         || p.Status == TokenPurchaseStatus.NeedsReconciliation))
            .SumAsync(p => (int?)p.Quantity, ct) ?? 0;

        return issued + reserved;
    }
}
