using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

namespace MLMConquerorGlobalEdition.Billing.Services.AnnualFee;

/// <summary>El token gastado: la instancia (con su id de siempre), su código y su tipo.</summary>
public sealed record ConsumedFeeToken(long TokenTransactionId, string Code, int TokenTypeId);

public interface IAnnualFeeTokenConsumer
{
    /// <summary>El token de cuota que se gastaría ahora (con seguimiento), o null.</summary>
    Task<TokenTransaction?> FindAsync(string memberId, DateTime now, CancellationToken ct = default);

    /// <summary>
    /// Lo gasta para el pedido: Used, asiento del ledger y saldo agregado. NO guarda: quien cobra lo hace
    /// en la misma transacción que el pedido, el pago y el estado, bajo el candado del miembro.
    /// </summary>
    Task<ConsumedFeeToken> ConsumeAsync(
        TokenTransaction instance, string orderId, string actor, DateTime now, CancellationToken ct = default);
}

/// <summary>
/// Gasta un token de cuota (Plan K, K8). Igual que el canje en el alta (TokenRedemptionService): la
/// instancia queda Used con el pedido, un asiento sin código registra el uso y el saldo agregado baja.
/// Distribuir y transferir tokens toman el mismo candado del miembro, así que la instancia no cambia de
/// dueño entre leerla y gastarla.
/// </summary>
public sealed class AnnualFeeTokenConsumer : IAnnualFeeTokenConsumer
{
    private readonly AppDbContext _db;

    public AnnualFeeTokenConsumer(AppDbContext db) => _db = db;

    public Task<TokenTransaction?> FindAsync(string memberId, DateTime now, CancellationToken ct = default) =>
        AnnualFeeTokenQueries.Available(_db, memberId, now).InSpendingOrder().FirstOrDefaultAsync(ct);

    public async Task<ConsumedFeeToken> ConsumeAsync(
        TokenTransaction instance, string orderId, string actor, DateTime now, CancellationToken ct = default)
    {
        var owner = instance.MemberId;
        var code  = instance.ReferenceId!;

        instance.Status         = TokenInstanceStatus.Used;
        instance.UsedByMemberId = owner;
        instance.UsedAt         = now;
        instance.UsedOnOrderId  = orderId;

        // Asiento puro (sin código): no aparece como instancia canjeable.
        _db.TokenTransactions.Add(new TokenTransaction
        {
            MemberId        = owner,
            TokenTypeId     = instance.TokenTypeId,
            TransactionType = TokenTransactionType.Used,
            Quantity        = 1,
            UsedByMemberId  = owner,
            UsedAt          = now,
            UsedOnOrderId   = orderId,
            Status          = TokenInstanceStatus.Used,
            ReferenceId     = null,
            CreatedBy       = actor,
            CreationDate    = now,
            Notes           = $"Token {code} used for the Biz Center annual fee (order {orderId})"
        });

        var balance = await _db.TokenBalances
            .FirstOrDefaultAsync(b => b.MemberId == owner && b.TokenTypeId == instance.TokenTypeId, ct);
        if (balance is not null && balance.Balance > 0)
        {
            balance.Balance       -= 1;
            balance.LastUpdateDate = now;
            balance.LastUpdateBy   = actor;
        }

        return new ConsumedFeeToken(instance.Id, code, instance.TokenTypeId);
    }
}
