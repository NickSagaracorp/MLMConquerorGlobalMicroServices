using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
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

    // Un migrationBuilder.Sql con lineas GO: cada lote en su propio EXEC (sin generar el script entero).

    private static IReadOnlyList<MigrationCommand> Comandos(string sql,
        MigrationsSqlGenerationOptions opciones = MigrationsSqlGenerationOptions.Idempotent)
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=.;Database=SinConexion;Integrated Security=true;").Options);
        return db.GetService<IMigrationsSqlGenerator>().Generate(new[] { new SqlOperation { Sql = sql } }, null, opciones);
    }

    private static string Normalizado(string s) => s.Replace("\r\n", "\n").Trim();

    [Fact]
    public void Una_linea_GO_separa_lotes_y_cada_uno_va_en_su_EXEC()
    {
        var c = Comandos("CREATE FUNCTION dbo.A() RETURNS int AS BEGIN RETURN 1 END\nGO\nCREATE FUNCTION dbo.B() RETURNS int AS BEGIN RETURN 2 END");

        c.Should().HaveCount(2);
        Normalizado(c[0].CommandText).Should().Be("EXEC(N'CREATE FUNCTION dbo.A() RETURNS int AS BEGIN RETURN 1 END');");
        Normalizado(c[1].CommandText).Should().Be("EXEC(N'CREATE FUNCTION dbo.B() RETURNS int AS BEGIN RETURN 2 END');");
    }

    [Theory]
    [InlineData("go")]
    [InlineData("  Go\t")]
    [InlineData("GO  ")]
    public void El_separador_es_GO_solo_en_su_linea_sin_importar_mayusculas_ni_espacios(string linea)
    {
        var c = Comandos($"SELECT 1;\r\n{linea}\r\nSELECT 'x';\r\n{linea}\r\n");

        c.Should().HaveCount(2, "el GO final no deja un lote vacio");
        Normalizado(c[0].CommandText).Should().Be("EXEC(N'SELECT 1;');");
        Normalizado(c[1].CommandText).Should().Be("EXEC(N'SELECT ''x'';');");
    }

    [Fact]
    public void Una_linea_de_texto_que_empieza_por_Go_no_separa()
    {
        var sql = "INSERT INTO KbArticles (Body) VALUES (N'Pasos:\nGo to Settings\nGO-live\nGOTO fin\nAlgo GO')";

        var c = Comandos(sql);

        c.Should().ContainSingle();
        Normalizado(c[0].CommandText).Should().Be($"EXEC(N'{sql.Replace("'", "''")}');");
    }

    [Fact]
    public void GO_con_contador_no_se_admite_en_el_script_idempotente()
    {
        var generar = () => Comandos("SELECT 1;\nGO 2\n");

        generar.Should().Throw<NotSupportedException>().WithMessage("*GO 2*");
    }

    [Fact]
    public void La_barra_invertida_al_final_de_linea_une_lineas_como_en_el_generador_de_EF()
    {
        const string sql = "SELECT N'uno \\\ndos', N'tres \\\r\ncuatro';";

        var idempotente = Comandos(sql);
        var tal_cual = Comandos(sql, MigrationsSqlGenerationOptions.Default);

        idempotente.Should().ContainSingle();
        Normalizado(idempotente[0].CommandText).Should().Be("EXEC(N'SELECT N''uno dos'', N''tres cuatro'';');");
        Normalizado(tal_cual.Single().CommandText).Should().Be("SELECT N'uno dos', N'tres cuatro';");
    }

    [Fact]
    public void Sin_idempotencia_el_GO_lo_trata_EF()
    {
        var c = Comandos("SELECT 1;\nGO\nSELECT 2;", MigrationsSqlGenerationOptions.Default);

        c.Select(x => Normalizado(x.CommandText)).Should().Equal("SELECT 1;", "SELECT 2;");
    }
}
