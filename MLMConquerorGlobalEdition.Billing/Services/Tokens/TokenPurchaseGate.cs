using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Services.Tokens;

/// <summary>Serializa las compras de UN miembro; las de miembros distintos no se esperan.</summary>
public interface ITokenPurchaseGate
{
    /// <summary>Abre una transacción y toma dentro el candado exclusivo del miembro. Se suelta al confirmar o deshacer.</summary>
    /// <exception cref="TokenPurchaseBusyException">Si no se obtuvo el candado a tiempo.</exception>
    Task<IDbContextTransaction> EnterAsync(string memberId, CancellationToken ct = default);
}

public sealed class TokenPurchaseBusyException(string memberId)
    : Exception($"Another token purchase for member {memberId} is in progress.");

/// <summary>
/// <c>sp_getapplock</c> con dueño Transaction: vive en SQL Server, así que vale con varias
/// instancias de Billing. Preferido a SERIALIZABLE (resolvería la carrera con un interbloqueo) y a
/// un contador con RowVersion (TokenBalance no tiene ni índice único ni RowVersion real).
/// </summary>
public sealed class SqlServerTokenPurchaseGate : ITokenPurchaseGate
{
    private const int LockTimeoutMs = 15000;

    private readonly AppDbContext _db;

    public SqlServerTokenPurchaseGate(AppDbContext db) => _db = db;

    public async Task<IDbContextTransaction> EnterAsync(string memberId, CancellationToken ct = default)
    {
        var tx       = await _db.Database.BeginTransactionAsync(ct);
        var resource = $"token-purchase:{memberId}";

        // ToListAsync sin componer: EF manda el SQL tal cual. Un FirstAsync lo envolvería en una
        // subconsulta y SQL Server rechazaría el DECLARE/EXEC.
        var result = await _db.Database
            .SqlQuery<int>($"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = {LockTimeoutMs}; SELECT @r AS [Value]")
            .ToListAsync(ct);

        if (result.Count == 1 && result[0] >= 0)
            return tx;

        await tx.RollbackAsync(ct);
        await tx.DisposeAsync();
        throw new TokenPurchaseBusyException(memberId);
    }
}
