using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

/// <summary>
/// Un [Fact] que se marca como SALTADO (no como pasado) cuando no hay SQL Server alcanzable, como en
/// Billing.Tests (SqlServerMemberFundsGateTests): el Skip se pone al descubrir las pruebas.
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

/// <summary>La base local de desarrollo (o la de MLM_TEST_SQLSERVER). Las pruebas escriben datos sintéticos propios y los borran.</summary>
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

    public static AppDbContext Db(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptores) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).AddInterceptors(interceptores).Options);
}
