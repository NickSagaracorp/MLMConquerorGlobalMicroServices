using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>
/// Plan P: serializa las escrituras del binario (DualTeamTree).
/// <list type="bullet">
/// <item>Una colocacion toma <c>dual-tree</c> en compartido y <c>dual-slot:{padre}:{lado}</c> en exclusivo.</item>
/// <item>Un movimiento o una retirada de subarbol toma <c>dual-tree</c> en exclusivo: espera a que terminen
/// las colocaciones y las detiene mientras reescribe rutas.</item>
/// </list>
/// El orden es siempre arbol y despues hueco, asi que no se interbloquean entre si. Dueno Transaction: los
/// candados se sueltan al confirmar o deshacer. Quien llama vuelve a comprobar el hueco DENTRO del candado;
/// el indice unico UX_DualTeamTree_ParentMemberId_Side es la red. Mismo patron que GenealogyChildrenGate y
/// MemberFundsGate. Fuera de SQL Server (InMemory en las pruebas) no hace nada.
/// </summary>
public static class DualTeamGate
{
    public const int DefaultLockTimeoutMs = 15000;
    public const string TreeResource = "dual-tree";

    public static string SlotResource(string parentMemberId, TreeSide side) => $"dual-slot:{parentMemberId}:{(int)side}";

    /// <summary>Candado de una colocacion en el hueco (padre, lado). Exige la transaccion de quien llama.</summary>
    /// <exception cref="DualTeamBusyException">Si no se obtuvo a tiempo.</exception>
    public static async Task EnterSlotAsync(AppDbContext db, string parentMemberId, TreeSide side,
        CancellationToken ct = default, int lockTimeoutMs = DefaultLockTimeoutMs)
    {
        if (!Applies(db)) return;
        await LockAsync(db, TreeResource, "Shared", lockTimeoutMs, ct);
        await LockAsync(db, SlotResource(parentMemberId, side), "Exclusive", lockTimeoutMs, ct);
    }

    /// <summary>Candado de un movimiento o retirada de subarbol. Exige la transaccion de quien llama.</summary>
    /// <exception cref="DualTeamBusyException">Si no se obtuvo a tiempo.</exception>
    public static async Task EnterTreeExclusiveAsync(AppDbContext db, CancellationToken ct = default,
        int lockTimeoutMs = DefaultLockTimeoutMs)
    {
        if (!Applies(db)) return;
        await LockAsync(db, TreeResource, "Exclusive", lockTimeoutMs, ct);
    }

    private static bool Applies(AppDbContext db)
    {
        if (!db.Database.IsSqlServer()) return false;
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("DualTeamGate needs the caller's transaction.");
        return true;
    }

    private static async Task LockAsync(AppDbContext db, string resource, string mode, int timeoutMs, CancellationToken ct)
    {
        // ToListAsync sin componer: EF manda el SQL tal cual (un FirstAsync lo envolveria).
        var r = await db.Database
            .SqlQuery<int>($"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {resource}, @LockMode = {mode}, @LockOwner = 'Transaction', @LockTimeout = {timeoutMs}; SELECT @r AS [Value]")
            .ToListAsync(ct);
        if (r.Count != 1 || r[0] < 0) throw new DualTeamBusyException(resource);
    }
}

public sealed class DualTeamBusyException(string resource)
    : Exception($"Another dual-tree write holds {resource}.");
