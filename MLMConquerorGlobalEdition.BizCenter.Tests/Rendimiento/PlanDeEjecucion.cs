using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Interceptors;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>Plan estimado (SHOWPLAN_XML) de una consulta, sin ejecutarla. Para comprobar seeks (R12).</summary>
public static class PlanDeEjecucion
{
    public static async Task<string> EstimadoAsync(string sql)
    {
        await using var conexion = new SqlConnection(SqlServerReportes.ConnectionString);
        await conexion.OpenAsync();
        await using (var activar = new SqlCommand("SET SHOWPLAN_XML ON", conexion))
            await activar.ExecuteNonQueryAsync();
        try
        {
            await using var consulta = new SqlCommand(sql, conexion);
            return (string?)await consulta.ExecuteScalarAsync() ?? string.Empty;
        }
        finally
        {
            // La conexión vuelve al pool: sin esto, la siguiente prueba que la reciba no ejecutaría
            // nada y solo obtendría planes.
            await using var desactivar = new SqlCommand("SET SHOWPLAN_XML OFF", conexion);
            await desactivar.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// El plan REAL (SET STATISTICS XML) con el que SQL Server ejecuta la consulta tal como la manda EF:
    /// el mismo comando (sp_executesql con parámetros, CreateDbCommand) con lo que le añaden los
    /// interceptores del contexto (OPTION (RECOMPILE) en las del equipo). ToQueryString la da con
    /// variables locales, y el optimizador trata distinto una variable que un parámetro (Plan M,
    /// desviación de la Tarea 13). Ejecuta la consulta: solo lectura.
    /// </summary>
    public static async Task<string> CompiladoAsync<T>(AppDbContext db, IQueryable<T> consulta)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var comando = consulta.CreateDbCommand();
            comando.CommandText = RecompileTaggedQueriesInterceptor.Apply(comando.CommandText);

            await using (var activar = comando.Connection!.CreateCommand())
            {
                activar.CommandText = "SET STATISTICS XML ON";
                await activar.ExecuteNonQueryAsync();
            }

            string? plan = null;
            await using (var lector = await comando.ExecuteReaderAsync())
            {
                do
                {
                    var esPlan = lector.FieldCount == 1 && lector.GetName(0).Contains("Showplan", StringComparison.Ordinal);
                    while (await lector.ReadAsync())
                        if (esPlan) plan = lector.GetString(0);
                }
                while (await lector.NextResultAsync());
            }

            await using (var desactivar = comando.Connection.CreateCommand())
            {
                desactivar.CommandText = "SET STATISTICS XML OFF";
                await desactivar.ExecuteNonQueryAsync();
            }
            return plan ?? string.Empty;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>
    /// Operadores del plan que recorren la tabla entera (o un índice entero) de <paramref name="tabla"/>;
    /// con <paramref name="alias"/>, solo los de ese alias de la consulta (p. ej. "[g]", la fila del equipo,
    /// y no la subconsulta del nodo vigente, que con un equipo grande se resuelve mejor con un hash).
    /// </summary>
    public static IReadOnlyList<string> Recorridos(string plan, string tabla, string? alias = null) =>
        XDocument.Parse(plan).Descendants()
            .Where(e => e.Name.LocalName == "RelOp"
                     && ((string?)e.Attribute("PhysicalOp"))?.EndsWith("Scan", StringComparison.Ordinal) == true
                     && e.Elements().Any(o => o.Name.LocalName is "IndexScan" or "TableScan"
                                           && o.Elements().Any(x => x.Name.LocalName == "Object"
                                                                 && (string?)x.Attribute("Table") == $"[{tabla}]"
                                                                 && (alias is null || (string?)x.Attribute("Alias") == alias))))
            .Select(e => (string)e.Attribute("PhysicalOp")!)
            .ToList();

    public const string Seek = "PhysicalOp=\"Index Seek\"";
}
