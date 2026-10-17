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
/// Fuera de SQL Server (InMemory en las pruebas) no hay candado: devuelve null.
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
        if (!db.Database.IsRelational())
            return null;
        if (db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException(
                "GenealogyChildrenGate needs the signup to run inside the execution strategy (EnableRetryOnFailure is on).");

        var tx = await db.Database.BeginTransactionAsync(ct);
        int code;
        try
        {
            var resource = Resource(sponsorMemberId);
            // ToListAsync sin componer: EF manda el SQL tal cual (un FirstAsync lo envolvería).
            var result = await db.Database
                .SqlQuery<int>($"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = {lockTimeoutMs}; SELECT @r AS [Value]")
                .ToListAsync(ct);
            code = result.Count == 1 ? result[0] : -999;
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

    private static async Task ReleaseAsync(IDbContextTransaction tx)
    {
        try { await tx.RollbackAsync(CancellationToken.None); }
        catch { /* conexión caída: SQL Server descarta la transacción por su cuenta */ }
        finally { await tx.DisposeAsync(); }
    }
}

public sealed class GenealogyChildrenBusyException(string? sponsorMemberId)
    : Exception($"Another signup under sponsor {sponsorMemberId ?? "(root)"} is in progress.");
