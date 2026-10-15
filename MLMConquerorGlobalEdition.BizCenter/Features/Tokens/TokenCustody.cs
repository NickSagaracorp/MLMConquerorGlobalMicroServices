using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Funds;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tokens;

/// <summary>
/// Lo que comparten distribuir (por cantidad) y transferir (por código): la regla del equipo, el
/// candado de los dos miembros y el movimiento del agregado <see cref="TokenBalance"/>. Una sola
/// copia para que las dos rutas no diverjan.
/// </summary>
internal static class TokenCustody
{
    /// <summary>
    /// ¿Cuelga <paramref name="recipientId"/> del que envía en el árbol de inscripción (patrocinio)?
    /// Se mira por <c>HierarchyPath</c>, que termina en barra: <c>/A/</c> no es prefijo de <c>/AB/</c>.
    /// Null si el que envía no tiene nodo (falla cerrado: quien llama lo rechaza).
    /// </summary>
    public static async Task<bool?> IsInEnrollmentTeamAsync(
        AppDbContext db, string senderId, string recipientId, CancellationToken ct)
    {
        var senderPath = await db.GenealogyTree.AsNoTracking()
            .Where(g => g.MemberId == senderId)
            .Select(g => g.HierarchyPath)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(senderPath))
            return null;

        return await db.GenealogyTree.AsNoTracking()
            .AnyAsync(g => g.MemberId == recipientId
                        && g.MemberId != senderId
                        && g.HierarchyPath.StartsWith(senderPath), ct);
    }

    /// <summary>
    /// Toma el candado de fondos de los dos miembros, en orden ordinal para que dos operaciones
    /// cruzadas (A→B y B→A) no se interbloqueen. Devuelve la transacción que los sostiene, o null si
    /// alguno estaba ocupado (la transacción ya se deshizo).
    /// </summary>
    public static async Task<IDbContextTransaction?> EnterBothAsync(
        IMemberFundsGate gate, string senderId, string recipientId, CancellationToken ct)
    {
        var (first, second) = string.CompareOrdinal(senderId, recipientId) <= 0
            ? (senderId, recipientId)
            : (recipientId, senderId);

        IDbContextTransaction tx;
        try
        {
            tx = await gate.EnterAsync(first, ct);
        }
        catch (MemberFundsBusyException)
        {
            return null;
        }

        bool acquired;
        try
        {
            acquired = await gate.TryAcquireAsync(
                second, TimeSpan.FromMilliseconds(SqlServerMemberFundsGate.DefaultLockTimeoutMs), ct);
        }
        catch
        {
            await ReleaseAsync(tx);
            throw;
        }

        if (acquired)
            return tx;

        await ReleaseAsync(tx);
        return null;
    }

    /// <summary>
    /// Baja el agregado del que envía (nunca por debajo de cero: los códigos mandan, el agregado es
    /// una caché que puede venir desfasada) y sube o crea el del que recibe. No guarda.
    /// </summary>
    public static async Task MoveBalanceAsync(
        AppDbContext db, string senderId, string recipientId, int tokenTypeId, int quantity,
        DateTime now, string? userId, CancellationToken ct)
    {
        var senderBalance = await db.TokenBalances
            .FirstOrDefaultAsync(tb => tb.MemberId == senderId && tb.TokenTypeId == tokenTypeId, ct);
        if (senderBalance is not null)
        {
            senderBalance.Balance        = Math.Max(0, senderBalance.Balance - quantity);
            senderBalance.LastUpdateDate = now;
            senderBalance.LastUpdateBy   = userId;
        }

        var recipientBalance = await db.TokenBalances
            .FirstOrDefaultAsync(tb => tb.MemberId == recipientId && tb.TokenTypeId == tokenTypeId, ct);
        if (recipientBalance is null)
        {
            recipientBalance = new TokenBalance
            {
                Id           = Guid.NewGuid().ToString(),
                MemberId     = recipientId,
                TokenTypeId  = tokenTypeId,
                Balance      = 0,
                CreatedBy    = userId ?? recipientId,
                CreationDate = now
            };
            db.TokenBalances.Add(recipientBalance);
        }
        recipientBalance.Add(quantity);
        recipientBalance.LastUpdateDate = now;
        recipientBalance.LastUpdateBy   = userId;
    }

    // Igual que la puerta: deshacer sin el token de la petición (puede ser justo el cancelado) y sin
    // tapar el error original si la conexión ya cayó.
    private static async Task ReleaseAsync(IDbContextTransaction tx)
    {
        try { await tx.RollbackAsync(CancellationToken.None); }
        catch { /* SQL Server descarta la transacción por su cuenta */ }
        finally { await tx.DisposeAsync(); }
    }
}
