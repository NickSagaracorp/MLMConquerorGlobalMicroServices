using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Migrations.Infraestructura;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Migraciones;

/// <summary>
/// Plan Q, Bloque 6: el script idempotente (DeploymentScripts/migrate-rds.sql) se puede ejecutar entero. Solo genera texto:
/// no abre ninguna conexion.
/// </summary>
public class ScriptIdempotenteTests
{
    private static string Script(MigrationsSqlGenerationOptions opciones = MigrationsSqlGenerationOptions.Idempotent)
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=.;Database=SinConexion;Integrated Security=true;").Options);
        return db.GetService<IMigrator>().GenerateScript(options: opciones);
    }

    private static readonly Regex ModuloDentroDeUnIf = new(
        @"^BEGIN\s*\r?\n\s*CREATE\s+(OR\s+ALTER\s+)?(FUNCTION|VIEW|PROCEDURE|PROC|TRIGGER)\b",
        RegexOptions.Multiline | RegexOptions.IgnoreCase);

    [Fact]
    public void Las_funciones_van_en_EXEC_porque_CREATE_FUNCTION_tiene_que_ser_la_primera_sentencia_del_lote()
    {
        var script = Script();

        ModuloDentroDeUnIf.Matches(script).Select(m => m.Value).Should().BeEmpty();
        Regex.IsMatch(script, @"EXEC\(N'\s*CREATE OR ALTER FUNCTION dbo\.GenealogyAncestors").Should().BeTrue();
    }

    [Fact]
    public void El_SQL_a_mano_va_en_EXEC_para_que_compile_aunque_use_columnas_que_anade_la_misma_migracion()
    {
        // AddSsnEncryptedAndCountryHasStates anade Countries.HasStates y la rellena con un UPDATE en la misma migracion.
        var script = Script();

        script.Should().Contain("EXEC(N'UPDATE Countries SET HasStates = 1");
        script.Should().NotContain("\n    UPDATE Countries SET HasStates = 1");
    }

    [Fact]
    public void Sin_idempotencia_las_funciones_se_crean_tal_cual()
    {
        Script(MigrationsSqlGenerationOptions.Default).Should().Contain("\nCREATE OR ALTER FUNCTION dbo.GenealogyAncestors");
    }

    [Fact]
    public void Las_guardas_de_tabla_previa_solo_estan_en_HelpdeskModule()
    {
        var script = Script();
        var lineas = script.Split('\n');
        var bloque = "";
        var fuera = new List<string>();
        foreach (var l in lineas)
        {
            var m = Regex.Match(l, @"\[MigrationId\] = N'(?<id>[^']+)'");
            if (m.Success) bloque = m.Groups["id"].Value;
            if (l.Contains(SoloSiLaTablaEsNueva.TablaDePrevias) && bloque != "20260405130705_HelpdeskModule")
                fuera.Add($"{bloque}: {l.Trim()}");
        }

        fuera.Should().BeEmpty();
        script.Should().Contain($"IF NOT EXISTS (SELECT 1 FROM dbo.[{SoloSiLaTablaEsNueva.TablaDePrevias}] WHERE Nombre = N'AspNetRoles')");
    }
}
