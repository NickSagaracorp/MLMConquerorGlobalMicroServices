using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Funds;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

/// <summary>
/// La puerta de fondos en pruebas del alta: la transacción (no-op en InMemory) sin el candado de SQL
/// Server. Anota por qué miembros se pasó, puede declarar ocupados a algunos y puede ejecutar algo
/// justo al entrar, que es lo que haría otra operación que terminó mientras el alta esperaba el
/// candado. La exclusión mutua real la prueban SqlServerMemberFundsGateTests (Billing.Tests).
/// </summary>
public sealed class PuertaDeFondosDePrueba(AppDbContext db, params string[] ocupados) : IMemberFundsGate
{
    public List<string> Entrados { get; } = new();
    public Func<Task>? AlEntrar { get; set; }

    public async Task<IDbContextTransaction> EnterAsync(string memberId, CancellationToken ct = default)
    {
        if (ocupados.Contains(memberId)) throw new MemberFundsBusyException(memberId);
        Entrados.Add(memberId);
        var tx = await db.Database.BeginTransactionAsync(ct);
        if (AlEntrar is not null) await AlEntrar();
        return tx;
    }

    public Task<bool> TryAcquireAsync(string memberId, TimeSpan timeout, CancellationToken ct = default)
    {
        if (ocupados.Contains(memberId)) return Task.FromResult(false);
        Entrados.Add(memberId);
        return Task.FromResult(true);
    }
}
