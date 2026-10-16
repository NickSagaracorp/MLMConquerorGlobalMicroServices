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
    }
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

    public static AppDbContext Db() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    /// <summary>Un MemberId sintético de 14 caracteres (cabe en los 20 de la columna) que no choca con nada real.</summary>
    public static string NuevoId(string prefijo = "TJ") =>
        (prefijo + Guid.NewGuid().ToString("N")).Substring(0, 14).ToUpperInvariant();
}
