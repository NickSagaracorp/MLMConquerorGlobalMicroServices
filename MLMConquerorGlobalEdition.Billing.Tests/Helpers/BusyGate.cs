using Microsoft.EntityFrameworkCore.Storage;
using MLMConquerorGlobalEdition.Repository.Services.Funds;

namespace MLMConquerorGlobalEdition.Billing.Tests.Helpers;

/// <summary>El candado de fondos siempre ocupado: otra operación del mismo miembro está en curso.</summary>
public sealed class BusyGate : IMemberFundsGate
{
    public Task<IDbContextTransaction> EnterAsync(string memberId, CancellationToken ct = default) =>
        throw new MemberFundsBusyException(memberId);

    public Task<bool> TryAcquireAsync(string memberId, TimeSpan timeout, CancellationToken ct = default) =>
        Task.FromResult(false);
}
