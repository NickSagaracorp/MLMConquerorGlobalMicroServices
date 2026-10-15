using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Funds;

namespace MLMConquerorGlobalEdition.Billing.Tests.Helpers;

/// <summary>
/// La puerta en pruebas: la transacción (no-op en InMemory) sin el candado de SQL Server, y anota
/// por qué miembros se pasó. La exclusión mutua real se comprueba contra SQL Server en
/// SqlServerMemberFundsGateTests y en la Tarea 16, paso 6.
/// </summary>
public sealed class TransactionOnlyGate(AppDbContext db) : IMemberFundsGate
{
    public List<string> Entered { get; } = new();

    public Task<IDbContextTransaction> EnterAsync(string memberId, CancellationToken ct = default)
    {
        Entered.Add(memberId);
        return db.Database.BeginTransactionAsync(ct);
    }

    public Task<bool> TryAcquireAsync(string memberId, TimeSpan timeout, CancellationToken ct = default)
    {
        Entered.Add(memberId);
        return Task.FromResult(true);
    }
}
