using Microsoft.Data.SqlClient;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

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
        await using var consulta = new SqlCommand(sql, conexion);
        return (string?)await consulta.ExecuteScalarAsync() ?? string.Empty;
    }

    public const string Seek = "PhysicalOp=\"Index Seek\"";
}
