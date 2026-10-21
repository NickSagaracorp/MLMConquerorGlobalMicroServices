using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Update;

namespace MLMConquerorGlobalEdition.Repository.Migrations.Infraestructura;

/// <summary>
/// Plan Q (DQ11): el generador de SQL Server de siempre, salvo para las operaciones marcadas con
/// SoloSiLaTablaEsNueva.Anotacion: cada comando suyo se envuelve en
/// IF NOT EXISTS (tabla en __PlanQ_TablasPrevias) EXEC(N'…'). EXEC aplaza la compilacion: un INSERT o un indice sobre
/// columnas que la tabla vieja no tiene no rompe el lote aunque no se ejecute.
///
/// Ademas, solo en el script idempotente (migrate-rds.sql), cada migrationBuilder.Sql va en EXEC(N'…'), como EF ya hace
/// con sus UpdateData e InsertData. El script mete cada sentencia en IF NOT EXISTS (historial) BEGIN … END y compila la
/// migracion entera en un lote: sin EXEC, un CREATE FUNCTION no es la primera sentencia del lote (156 y 319) y un UPDATE
/// sobre una columna que la misma migracion acaba de anadir no compila (207) y tira el lote. Con MigrateAsync no cambia
/// nada: cada operacion es su propio comando.
///
/// Un Sql con lineas GO se parte en lotes y cada lote va en su propio EXEC. Separa solo una linea que es GO y nada mas
/// (sin distinguir mayusculas, con espacios o tabuladores alrededor): "Go to Settings", "GO-live" o "GOTO" dentro de un
/// literal no separan. Como sqlcmd, una linea GO dentro de un literal de varias lineas si separa. GO con contador
/// ("GO 2") no se admite y la generacion falla. La barra invertida al final de una linea la une con la siguiente, como
/// en el generador de EF.
/// </summary>
public sealed class MigracionesSqlServerConGuardas : SqlServerMigrationsSqlGenerator
{
    private static readonly Regex LineaGo = new(@"^[ \t]*GO[ \t]*\r?(?:\n|\z)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
    private static readonly Regex LineaGoConContador = new(@"^[ \t]*GO[ \t]+\d+[ \t]*\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

    public MigracionesSqlServerConGuardas(MigrationsSqlGeneratorDependencies dependencies, ICommandBatchPreparer commandBatchPreparer)
        : base(dependencies, commandBatchPreparer)
    {
    }

    protected override void Generate(SqlOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        if (!Options.HasFlag(MigrationsSqlGenerationOptions.Idempotent))
        {
            base.Generate(operation, model, builder);
            return;
        }
        foreach (var lote in Lotes(operation.Sql))
            builder
                .Append("EXEC(N'")
                .Append(lote.Replace("'", "''"))
                .Append("')")
                .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator)
                .EndCommand(operation.SuppressTransaction);
    }

    /// <summary>Los lotes de un Sql: separados por lineas GO, sin los vacios. Sin GO, el texto tal cual.</summary>
    private static IEnumerable<string> Lotes(string sql)
    {
        var texto = sql.Replace("\\\n", "").Replace("\\\r\n", "");
        if (LineaGoConContador.Match(texto) is { Success: true } conContador)
            throw new NotSupportedException(
                $"'{conContador.Value.Trim()}' en un migrationBuilder.Sql: el script idempotente solo admite lineas GO sin contador.");
        if (!LineaGo.IsMatch(texto))
            return new[] { texto };
        return LineaGo.Split(texto)
            .Select(l => l.EndsWith("\r\n") ? l[..^2] : l.EndsWith('\n') ? l[..^1] : l)
            .Where(l => !string.IsNullOrWhiteSpace(l));
    }

    public override IReadOnlyList<MigrationCommand> Generate(IReadOnlyList<MigrationOperation> operations, IModel? model = null,
        MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        if (!operations.Any(o => o.FindAnnotation(SoloSiLaTablaEsNueva.Anotacion) is not null))
            return base.Generate(operations, model, options);

        var resultado = new List<MigrationCommand>();
        foreach (var op in operations)
        {
            var comandos = base.Generate(new[] { op }, model, options);
            if (op.FindAnnotation(SoloSiLaTablaEsNueva.Anotacion) is null || SoloSiLaTablaEsNueva.Tabla(op) is not { } tabla)
            {
                resultado.AddRange(comandos);
                continue;
            }
            foreach (var c in comandos)
            {
                var texto = $"IF NOT EXISTS (SELECT 1 FROM dbo.[{SoloSiLaTablaEsNueva.TablaDePrevias}] WHERE Nombre = N'{tabla}')\n"
                          + $"    EXEC(N'{c.CommandText.Replace("'", "''")}');\n";
                var comando = Dependencies.CommandBuilderFactory.Create().Append(texto).Build();
                resultado.Add(new MigrationCommand(comando, Dependencies.CurrentContext.Context, c.CommandLogger, c.TransactionSuppressed));
            }
        }
        return resultado;
    }
}
