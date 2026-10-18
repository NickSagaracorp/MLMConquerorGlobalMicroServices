using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;

/// <summary>
/// Un [Fact] que se marca como SALTADO (no como pasado) cuando no hay SQL Server alcanzable, como en
/// BizCenter.Tests y Signups.Tests: el Skip se pone al descubrir las pruebas.
/// </summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (!SqlServerTestDb.Reachable)
            Skip = "SQL Server no alcanzable (define MLM_TEST_SQLSERVER para usar otra instancia).";
        else if (!SqlServerTestDb.Migrada)
            Skip = "La base no tiene la migracion ClaveCompactaDelBinario del Plan P (se aplica tras reparar el binario): define MLM_TEST_SQLSERVER con una base migrada.";
    }
}

/// <summary>La base local de desarrollo (o la de MLM_TEST_SQLSERVER). Las pruebas escriben datos propios y los borran.</summary>
public static class SqlServerTestDb
{
    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("MLM_TEST_SQLSERVER")
        ?? "Server=.;Database=MLMConquerorGlobalEditionDb;integrated security=true;TrustServerCertificate=True;Encrypt=True;Connect Timeout=3;";

    private static readonly Lazy<bool> LazyReachable = new(() =>
    {
        try
        {
            using var c = new SqlConnection(ConnectionString);
            c.Open();
            return true;
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            return false;
        }
    });

    public static bool Reachable => LazyReachable.Value;

    /// <summary>
    /// Plan P: el modelo ya tiene las columnas de la clave compacta del binario. Contra una base sin la migracion
    /// ClaveCompactaDelBinario (la local hasta que se repara), cualquier consulta de EF sobre DualTeamTree falla
    /// por columna inexistente: las pruebas se saltan con este motivo en vez de fallar.
    /// </summary>
    private static readonly Lazy<bool> LazyMigrada = new(() =>
    {
        try
        {
            using var c = new SqlConnection(ConnectionString);
            c.Open();
            using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId LIKE '%[_]ClaveCompactaDelBinario'", c);
            return (int)cmd.ExecuteScalar()! > 0;
        }
        catch (SqlException)
        {
            return false;
        }
    });

    public static bool Migrada => LazyMigrada.Value;

    public static AppDbContext Db(params IInterceptor[] interceptores) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).AddInterceptors(interceptores).Options);
}

/// <summary>
/// Plan P: hace coincidir dos escritores del binario justo antes de guardar un nodo con padre. Sin candado,
/// los dos pasan la comprobacion del hueco e insertan; con candado, el segundo espera al primero, que sigue
/// al agotar <see cref="Espera"/>. (Misma idea que BizCenter.Tests/Binario/Encuentro.)
/// </summary>
public sealed class Encuentro(int escritores = 2) : SaveChangesInterceptor
{
    public static readonly TimeSpan Espera = TimeSpan.FromSeconds(3);
    private readonly CountdownEvent _llegadas = new(escritores);

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData e, InterceptionResult<int> result, CancellationToken ct = default)
    {
        var insertaNodo = e.Context!.ChangeTracker.Entries<DualTeamEntity>()
            .Any(x => x.State == EntityState.Added && x.Entity.ParentMemberId != null);
        if (insertaNodo)
        {
            try { _llegadas.Signal(); } catch (InvalidOperationException) { /* ya estaban todos */ }
            await Task.Run(() => _llegadas.Wait(Espera), ct);
        }
        return result;
    }
}
