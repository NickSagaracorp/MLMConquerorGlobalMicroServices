using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Tests.Helpers;

/// <summary>
/// La puerta en pruebas: la transacción (no-op en InMemory) sin el candado de SQL Server. La
/// exclusión mutua real se comprueba contra SQL Server en la Tarea 16, paso 6.
/// </summary>
public sealed class TransactionOnlyGate(AppDbContext db) : ITokenPurchaseGate
{
    public Task<IDbContextTransaction> EnterAsync(string memberId, CancellationToken ct = default) =>
        db.Database.BeginTransactionAsync(ct);
}
