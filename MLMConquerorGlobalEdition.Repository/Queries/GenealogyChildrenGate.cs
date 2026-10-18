using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Plan M (revisión I3): serializa las altas bajo un mismo patrocinador, para que dos no calculen el
/// mismo Node con <see cref="GenealogyNodes.NextChildAsync"/>. Mismo patrón que MemberFundsGate:
/// sp_getapplock con dueño Transaction sobre <c>genealogy-children:{patrocinador}</c>; el candado se
/// suelta al confirmar o deshacer. Las altas de patrocinadores distintos no se esperan. El índice
/// único IX_GenealogyTree_Node y el reintento por IsDuplicateNode quedan como red de seguridad.
/// </summary>
/// <remarks>
/// La transacción la abre el alta y abarca NextChildAsync y el SaveChanges. SignupAPI no configura
/// EnableRetryOnFailure; si un día lo hace, EF rechazará esta transacción iniciada por el usuario y el
/// alta tendrá que ir entera dentro de Database.CreateExecutionStrategy().ExecuteAsync (el bucle del
/// alta ya reconstruye todo en cada intento). Se comprueba aquí para que falle con un mensaje claro.
/// Fuera de SQL Server (InMemory en las pruebas) no hay candado: devuelve null. Con una transacción ya
/// abierta por quien llama, el candado se toma dentro de ella y también devuelve null (revisión M-d).
/// </remarks>
public static class GenealogyChildrenGate
{
    public const int DefaultLockTimeoutMs = 15000;

    /// <summary>El nombre del candado; las raíces nuevas comparten uno.</summary>
    public static string Resource(string? sponsorMemberId) => $"genealogy-children:{sponsorMemberId ?? "(root)"}";

    /// <summary>Abre una transacción y toma dentro el candado del patrocinador. Null fuera de SQL Server.</summary>
    /// <exception cref="GenealogyChildrenBusyException">Si no se obtuvo el candado a tiempo.</exception>
    public static async Task<IDbContextTransaction?> EnterAsync(
        AppDbContext db, string? sponsorMemberId, CancellationToken ct = default, int lockTimeoutMs = DefaultLockTimeoutMs)
    {
        if (!db.Database.IsSqlServer())
            return null;

        // Revisión M-d: si quien llama ya tiene una transacción abierta, el candado se toma DENTRO de ella
        // (dueño Transaction: se suelta cuando esa transacción confirme o deshaga) y se devuelve null: no se
        // abre otra ni se confirma aquí, porque la transacción es de quien llama.
        if (db.Database.CurrentTransaction is not null)
        {
            if (await GetAppLockAsync(db, sponsorMemberId, lockTimeoutMs, ct) < 0)
                throw new GenealogyChildrenBusyException(sponsorMemberId);
            return null;
        }
        if (db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException(
                "GenealogyChildrenGate needs the signup to run inside the execution strategy (EnableRetryOnFailure is on).");

        var tx = await db.Database.BeginTransactionAsync(ct);
        int code;
        try
        {
            code = await GetAppLockAsync(db, sponsorMemberId, lockTimeoutMs, ct);
        }
        catch
        {
            await ReleaseAsync(tx);
            throw;
        }

        if (code >= 0)
            return tx;

        await ReleaseAsync(tx);
        throw new GenealogyChildrenBusyException(sponsorMemberId);
    }

    /// <summary>0 o 1 = concedido; negativo = tiempo agotado, cancelado, interbloqueo o error.</summary>
    private static async Task<int> GetAppLockAsync(AppDbContext db, string? sponsorMemberId, int lockTimeoutMs, CancellationToken ct)
    {
        var resource = Resource(sponsorMemberId);
        // ToListAsync sin componer: EF manda el SQL tal cual (un FirstAsync lo envolvería).
        var result = await db.Database
            .SqlQuery<int>($"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = {lockTimeoutMs}; SELECT @r AS [Value]")
            .ToListAsync(ct);
        return result.Count == 1 ? result[0] : -999;
    }

    private static async Task ReleaseAsync(IDbContextTransaction tx)
    {
        try { await tx.RollbackAsync(CancellationToken.None); }
        catch { /* conexión caída: SQL Server descarta la transacción por su cuenta */ }
        finally { await tx.DisposeAsync(); }
    }
}

public sealed class GenealogyChildrenBusyException(string? sponsorMemberId)
    : Exception($"Another signup under sponsor {sponsorMemberId ?? "(root)"} is in progress.");
