using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using MLMConquerorGlobalEdition.Repository.Queries;

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
    /// Se mira por <c>Node</c> (Plan M): el destinatario cuelga del Node del que envía, sin las falsas
    /// coincidencias de un prefijo de texto. Si el que envía tuviera más de un nodo, manda el más reciente.
    /// Devuelve el MemberId del que envía TAL COMO ESTÁ GUARDADO (el que viene del token puede diferir
    /// en mayúsculas): es el que nombra su candado y el que deben usar las consultas y los asientos.
    /// <c>SenderId</c> null si el que envía no tiene nodo (falla cerrado: quien llama lo rechaza).
    /// </summary>
    public static async Task<TeamCheck> IsInEnrollmentTeamAsync(
        AppDbContext db, string senderId, string recipientId, CancellationToken ct)
    {
        var sender = await db.GenealogyTree.AsNoTracking()
            .Where(g => g.MemberId == senderId)
            .OrderByDescending(g => g.CreationDate)
            .ThenByDescending(g => g.Id)      // nodo vigente con desempate por Id (revisión M-e)
            .Select(g => new { g.MemberId, g.Node })
            .FirstOrDefaultAsync(ct);
        if (sender is null || sender.Node is null)
            return new TeamCheck(null, false);

        var senderRoot = sender.Node;
        var storedSender = sender.MemberId;

        var inTeam = await db.GenealogyTree.AsNoTracking()
            .CurrentUnder(senderRoot)
            .AnyAsync(g => g.MemberId == recipientId
                        && g.MemberId != storedSender, ct);
        return new TeamCheck(storedSender, inTeam);
    }

    /// <summary>Resultado de <see cref="IsInEnrollmentTeamAsync"/>.</summary>
    public readonly record struct TeamCheck(string? SenderId, bool InTeam);

    /// <summary>
    /// El destinatario tal como está guardado (su id nombra su candado y es el que escribe Billing; uno
    /// escrito con otras mayúsculas sería otro recurso) y si su cuenta está activa. Null si no existe o
    /// está borrado.
    /// </summary>
    public static async Task<Recipient?> FindRecipientAsync(AppDbContext db, string requested, CancellationToken ct)
    {
        var found = await db.MemberProfiles.AsNoTracking()
            .Where(m => m.MemberId == requested && !m.IsDeleted)
            .Select(m => new { m.MemberId, m.Status })
            .FirstOrDefaultAsync(ct);
        return found is null ? null : new Recipient(found.MemberId, found.Status == MemberAccountStatus.Active);
    }

    /// <summary>Resultado de <see cref="FindRecipientAsync"/>.</summary>
    public sealed record Recipient(string MemberId, bool IsActive);

    /// <summary>
    /// Guarda y confirma la transacción del candado. Si el guardado choca con los tokens de
    /// concurrencia de <see cref="TokenTransaction"/> (dueño y estado: otro proceso que no toma este
    /// candado, como un canje en el alta con el dueño de antes, cambió un código entre la lectura y el
    /// guardado), deshace, vacía el contexto y devuelve false: quien llama responde
    /// <c>PURCHASE_BUSY</c>, que se puede repetir.
    /// </summary>
    public static async Task<bool> SaveAndCommitAsync(AppDbContext db, IDbContextTransaction tx, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            try { await tx.RollbackAsync(CancellationToken.None); }
            catch { /* SQL Server descarta la transacción por su cuenta */ }
            return false;
        }

        await tx.CommitAsync(ct);
        return true;
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
