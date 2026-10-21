using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Migraciones;

/// <summary>
/// Plan Q: el artefacto de produccion, DeploymentScripts/migrate-rds.sql, es el que se generaria hoy y se ejecuta con
/// sqlcmd -b -I (como dice el despliegue) sobre una base vacia sin errores, y una segunda pasada no cambia ni el esquema
/// ni los datos. Solo crea y borra MLMConquerorGlobalEditionDb_PruebaMigraciones_Script y _Modelo en la instancia local.
/// </summary>
public class ScriptDeDespliegueTests
{
    private const string Script = "MLMConquerorGlobalEditionDb_PruebaMigraciones_Script";
    private const string Modelo = "MLMConquerorGlobalEditionDb_PruebaMigraciones_Modelo";

    private static string Fichero()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var f = Path.Combine(d.FullName, "DeploymentScripts", "migrate-rds.sql");
            if (File.Exists(f)) return f;
        }
        throw new FileNotFoundException("DeploymentScripts/migrate-rds.sql");
    }

    private static string SinBomNiCrLf(string s) => s.TrimStart('﻿').Replace("\r\n", "\n");

    [DesdeCeroFact]
    public async Task El_script_de_despliegue_esta_al_dia_se_ejecuta_con_sqlcmd_y_una_segunda_pasada_no_cambia_nada()
    {
        var fichero = Fichero();
        string generado;
        int migraciones;
        await using (var db = MigracionesDesdeCeroTests.Contexto(Script))
        {
            generado = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
            migraciones = db.Database.GetMigrations().Count();
        }
        SinBomNiCrLf(await File.ReadAllTextAsync(fichero)).Should().Be(SinBomNiCrLf(generado),
            "migrate-rds.sql se regenera con dotnet ef migrations script --idempotent tras cada migracion o cambio del generador");

        await MigracionesDesdeCeroTests.BorrarAsync(Script);
        await MigracionesDesdeCeroTests.BorrarAsync(Modelo);
        try
        {
            await CrearAsync(Script);

            Sqlcmd(Script, fichero);
            var primera = await FotoAsync(Script);
            primera.Should().Contain(f => f.StartsWith($"__EFMigrationsHistory filas:{migraciones}:"), "se aplican todas las migraciones");
            primera.Should().Contain(f => f.Contains(" fk ") && f.EndsWith(":CASCADE"), "la foto lleva las claves foraneas");
            primera.Should().Contain(f => f.StartsWith("KbArticles filas:") && !f.StartsWith("KbArticles filas:0:"), "y los datos sembrados");

            Sqlcmd(Script, fichero);
            var segunda = await FotoAsync(Script);
            segunda.Except(primera).Order().Should().BeEmpty("la segunda pasada no crea ni cambia nada");
            primera.Except(segunda).Order().Should().BeEmpty("la segunda pasada no quita ni cambia nada");

            await using (var db = MigracionesDesdeCeroTests.Contexto(Modelo)) await db.Database.EnsureCreatedAsync();
            var porScript = await MigracionesDesdeCeroTests.EsquemaAsync(Script);
            var modelo = await MigracionesDesdeCeroTests.EsquemaAsync(Modelo);
            porScript.Except(modelo).Where(d => !MigracionesDesdeCeroTests.Excepciones.ContainsKey(d)).Order().Should()
                .BeEmpty("lo que el script crea de mas o distinto del modelo");
            modelo.Except(porScript).Where(d => !MigracionesDesdeCeroTests.Excepciones.ContainsKey(d)).Order().Should()
                .BeEmpty("lo que el modelo tiene y el script no crea");
        }
        finally
        {
            await MigracionesDesdeCeroTests.BorrarAsync(Script);
            await MigracionesDesdeCeroTests.BorrarAsync(Modelo);
        }
    }

    private static void Sqlcmd(string bd, string fichero)
    {
        var psi = new ProcessStartInfo("sqlcmd")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in new[] { "-S", ".", "-d", bd, "-E", "-C", "-b", "-I", "-i", fichero }) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var error = p.StandardError.ReadToEndAsync();
        var salida = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        var todo = salida + error.Result;
        p.ExitCode.Should().Be(0, todo);
        todo.Should().NotContain("Msg ", "el script no da ningun error");
    }

    private static async Task CrearAsync(string bd)
    {
        await using var c = new SqlConnection(MigracionesDesdeCeroTests.Cadena("master"));
        await c.OpenAsync();
        await using var cmd = new SqlCommand($"CREATE DATABASE [{bd}]", c);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// El esquema (<see cref="MigracionesDesdeCeroTests.EsquemaAsync"/>), los modulos y secuencias con su definicion, y
    /// por tabla (historial incluido) el numero de filas y CHECKSUM_AGG(BINARY_CHECKSUM(*)).
    /// </summary>
    private static async Task<HashSet<string>> FotoAsync(string bd)
    {
        const string sql = @"
SELECT CONCAT(o.name, ' ', o.type COLLATE DATABASE_DEFAULT, ':', CHECKSUM(m.definition))
FROM sys.objects o LEFT JOIN sys.sql_modules m ON m.object_id = o.object_id
WHERE o.type IN ('IF', 'FN', 'TF', 'P', 'V', 'TR', 'SO');
DECLARE @q nvarchar(max) = (
    SELECT STRING_AGG(CAST(CONCAT('SELECT CONCAT(N', QUOTENAME(t.name, ''''), ', '' filas:'', COUNT_BIG(*), '':'', ',
        'CHECKSUM_AGG(BINARY_CHECKSUM(*))) FROM ', QUOTENAME(s.name), '.', QUOTENAME(t.name)) AS nvarchar(max)), ' UNION ALL ')
    FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id);
EXEC(@q);";
        var foto = await MigracionesDesdeCeroTests.EsquemaAsync(bd);
        await using var c = new SqlConnection(MigracionesDesdeCeroTests.Cadena(bd));
        await c.OpenAsync();
        await using var cmd = new SqlCommand(sql, c) { CommandTimeout = 600 };
        await using var r = await cmd.ExecuteReaderAsync();
        do
        {
            while (await r.ReadAsync()) foto.Add(r.GetString(0));
        } while (await r.NextResultAsync());
        return foto;
    }
}
