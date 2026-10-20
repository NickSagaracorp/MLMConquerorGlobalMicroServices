using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Un [Fact] que se marca como SALTADO (no como pasado) cuando no hay SQL Server alcanzable, igual
/// que en Billing.Tests y Signups.Tests: el Skip se pone al descubrir las pruebas.
/// </summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (!SqlServerReportes.Reachable)
            Skip = "SQL Server no alcanzable (define MLM_TEST_SQLSERVER para usar otra instancia).";
        else if (!SqlServerReportes.Migrada)
            Skip = "La base no tiene la migracion ClaveCompactaDelBinario del Plan P (se aplica tras reparar el binario): define MLM_TEST_SQLSERVER con una base migrada.";
    }
}

/// <summary>Como <see cref="SqlServerFactAttribute"/>, para pruebas con datos.</summary>
public sealed class SqlServerTheoryAttribute : TheoryAttribute
{
    public SqlServerTheoryAttribute()
    {
        if (!SqlServerReportes.Reachable)
            Skip = "SQL Server no alcanzable (define MLM_TEST_SQLSERVER para usar otra instancia).";
        else if (!SqlServerReportes.Migrada)
            Skip = "La base no tiene la migracion ClaveCompactaDelBinario del Plan P: define MLM_TEST_SQLSERVER con una base migrada.";
    }
}

/// <summary>
/// Colección de las pruebas de reportes contra SQL Server: xUnit no las ejecuta en paralelo entre
/// sí, para que no compitan por las mismas tablas ni ensucien la medición de la raíz (R5/R16).
/// </summary>
[CollectionDefinition(Nombre, DisableParallelization = true)]
public sealed class SqlServerReportesCollection
{
    public const string Nombre = "SqlServerReportes";
}

/// <summary>
/// La base local de desarrollo (o la de MLM_TEST_SQLSERVER). Cada prueba abre una transacción,
/// siembra con <see cref="ReportesDb"/>, consulta con el mismo contexto y la deshace: no deja datos.
/// Necesita las migraciones del plan aplicadas en local (paso explícito de cada tarea de migración).
/// </summary>
public static class SqlServerReportes
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

    public static AppDbContext Db() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    /// <summary>
    /// Repite la prueba entera (con su transacción) si SQL Server la elige víctima de un deadlock
    /// (error 1205), hasta cinco intentos. Solo cubre eso: con servicios levantados contra la base
    /// local, una lectura que recorre Orders u OrderDetails sin índice (R16) choca con sus
    /// inserciones; no es un fallo de la lógica que se prueba. Cualquier otro error sale al primero.
    /// </summary>
    public static async Task ConReintentoAsync(Func<Task> prueba, int intentos = 5)
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                await prueba();
                return;
            }
            catch (SqlException ex) when (ex.Number == 1205 && intento < intentos)
            {
                await Task.Delay(1000 * intento);
            }
        }
    }

    /// <summary>Un MemberId sintético de 14 caracteres (cabe en los 20 de la columna) que no choca con nada real.</summary>
    public static string NuevoId(string prefijo = "TJ") =>
        (prefijo + Guid.NewGuid().ToString("N")).Substring(0, 14).ToUpperInvariant();
}
