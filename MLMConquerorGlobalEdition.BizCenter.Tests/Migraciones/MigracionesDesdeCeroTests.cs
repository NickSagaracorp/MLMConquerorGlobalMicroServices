using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Migraciones;

public sealed class DesdeCeroFactAttribute : FactAttribute
{
    public DesdeCeroFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MLM_MIGRACIONES_DESDE_CERO") != "1")
            Skip = "Define MLM_MIGRACIONES_DESDE_CERO=1: crea y borra MLMConquerorGlobalEditionDb_DesdeCero y _Modelo en la instancia local.";
        else if (!SqlServerReportes.Reachable)
            Skip = "SQL Server no alcanzable.";
    }
}

/// <summary>
/// Plan Q, Bloque 6: una base vacia se construye con las migraciones y queda igual que el modelo (EnsureCreated). No toca
/// la base local ni su historial: solo las dos bases de esta prueba, que se borran al final.
/// </summary>
public class MigracionesDesdeCeroTests
{
    private const string DesdeCero = "MLMConquerorGlobalEditionDb_DesdeCero";
    private const string Modelo = "MLMConquerorGlobalEditionDb_Modelo";
    private static readonly string[] Permitidas = { DesdeCero, Modelo };

    private static string Cadena(string bd) =>
        $"Server=.;Database={bd};Integrated Security=true;TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=true;";

    private static AppDbContext Contexto(string bd) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(Cadena(bd), o => o.CommandTimeout(600)).Options);

    [DesdeCeroFact]
    public async Task Una_base_vacia_se_construye_con_las_migraciones_e_igual_al_modelo()
    {
        await BorrarAsync(DesdeCero);
        await BorrarAsync(Modelo);
        try
        {
            await using (var db = Contexto(DesdeCero)) await db.Database.MigrateAsync();
            await using (var db = Contexto(Modelo)) await db.Database.EnsureCreatedAsync();

            var migrada = await EsquemaAsync(DesdeCero);
            var modelo = await EsquemaAsync(Modelo);
            migrada.Except(modelo).Order().Should().BeEmpty("lo que las migraciones crean de mas o distinto del modelo");
            modelo.Except(migrada).Order().Should().BeEmpty("lo que el modelo tiene y las migraciones no crean");
            (await ObjetosAsync(DesdeCero)).Should().Contain(new[]
                { "DualChildKey", "Numeros", "DualAncestorKeys", "GenealogyAncestors", "DualTeamRootOrdinal" });
        }
        finally
        {
            await BorrarAsync(DesdeCero);
            await BorrarAsync(Modelo);
        }
    }

    /// <summary>Columnas (tipo, longitud, nulos) e indices (unico, filtro) de las tablas de usuario, sin el historial.</summary>
    private static async Task<HashSet<string>> EsquemaAsync(string bd)
    {
        const string sql = @"
SELECT CONCAT(c.TABLE_NAME, '.', c.COLUMN_NAME, ':', c.DATA_TYPE, ':', c.CHARACTER_MAXIMUM_LENGTH, ':', c.IS_NULLABLE)
FROM INFORMATION_SCHEMA.COLUMNS c WHERE c.TABLE_NAME <> '__EFMigrationsHistory'
UNION ALL
SELECT CONCAT(t.name, ' ix ', i.name, ':', i.is_unique, ':', i.filter_definition)
FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
WHERE i.name IS NOT NULL AND t.name <> '__EFMigrationsHistory';";
        await using var c = new SqlConnection(Cadena(bd));
        await c.OpenAsync();
        await using var cmd = new SqlCommand(sql, c);
        await using var r = await cmd.ExecuteReaderAsync();
        var s = new HashSet<string>();
        while (await r.ReadAsync()) s.Add(r.GetString(0));
        return s;
    }

    private static async Task<List<string>> ObjetosAsync(string bd)
    {
        await using var c = new SqlConnection(Cadena(bd));
        await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT name FROM sys.objects WHERE type IN ('IF', 'FN', 'TF', 'SO')", c);
        await using var r = await cmd.ExecuteReaderAsync();
        var l = new List<string>();
        while (await r.ReadAsync()) l.Add(r.GetString(0));
        return l;
    }

    private static async Task BorrarAsync(string bd)
    {
        if (!Permitidas.Contains(bd)) throw new InvalidOperationException($"{bd} no es una base de esta prueba.");
        SqlConnection.ClearAllPools();
        await using var c = new SqlConnection("Server=.;Database=master;Integrated Security=true;TrustServerCertificate=True;Encrypt=True;");
        await c.OpenAsync();
        await using var cmd = new SqlCommand(
            $"IF DB_ID(N'{bd}') IS NOT NULL BEGIN ALTER DATABASE [{bd}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{bd}]; END", c);
        await cmd.ExecuteNonQueryAsync();
    }
}
