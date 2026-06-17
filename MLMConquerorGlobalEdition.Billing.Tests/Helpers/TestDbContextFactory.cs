using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Tests.Helpers;

/// <summary>
/// Creates an isolated in-memory AppDbContext per test to avoid state leakage.
/// TransactionIgnoredWarning is suppressed: EF InMemory does not support real transactions,
/// but the production code uses them for correctness on real SQL Server. Tests verify
/// business behaviour; the transaction boundary is a no-op under InMemory.
/// </summary>
public static class TestDbContextFactory
{
    public static AppDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }
}
