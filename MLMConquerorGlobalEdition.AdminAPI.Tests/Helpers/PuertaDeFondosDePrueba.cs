using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Funds;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;

/// <summary>
/// La puerta de fondos en pruebas del admin: la transacción (no-op en InMemory) sin el candado de SQL
/// Server. Anota por qué miembros se pasó y puede declarar ocupados a algunos. La exclusión mutua real
/// la prueban SqlServerMemberFundsGateTests (Billing.Tests).
/// </summary>
public sealed class PuertaDeFondosDePrueba(AppDbContext db, params string[] ocupados) : IMemberFundsGate
{
    public List<string> Entrados { get; } = new();

    public Task<IDbContextTransaction> EnterAsync(string memberId, CancellationToken ct = default)
    {
        if (ocupados.Contains(memberId)) throw new MemberFundsBusyException(memberId);
        Entrados.Add(memberId);
        return db.Database.BeginTransactionAsync(ct);
    }

    public Task<bool> TryAcquireAsync(string memberId, TimeSpan timeout, CancellationToken ct = default)
    {
        if (ocupados.Contains(memberId)) return Task.FromResult(false);
        Entrados.Add(memberId);
        return Task.FromResult(true);
    }
}
