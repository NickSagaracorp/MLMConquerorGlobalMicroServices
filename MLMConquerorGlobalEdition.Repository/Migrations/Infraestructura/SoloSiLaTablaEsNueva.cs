using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace MLMConquerorGlobalEdition.Repository.Migrations.Infraestructura;

/// <summary>
/// Plan Q (DQ11): una migracion que se genero contra una instantanea vacia (HelpdeskModule) vuelve a crear tablas que ya
/// crearon las anteriores. En las bases reales se resolvio a mano con fix_helpdesk_tables.sql (solo las tablas nuevas). Aqui
/// se hace lo mismo de forma automatica: al empezar se guarda la lista de tablas que ya existen, y cada operacion marcada
/// que toca una de ellas no se ejecuta. MigracionesSqlServerConGuardas pone la guarda.
/// </summary>
public static class SoloSiLaTablaEsNueva
{
    public const string Anotacion = "PlanQ:SoloSiLaTablaEsNueva";
    public const string TablaDePrevias = "__PlanQ_TablasPrevias";

    public static void Empezar(MigrationBuilder mb) => mb.Sql($@"
IF OBJECT_ID(N'dbo.{TablaDePrevias}') IS NOT NULL DROP TABLE dbo.{TablaDePrevias};
SELECT name COLLATE DATABASE_DEFAULT AS Nombre INTO dbo.{TablaDePrevias} FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo');");

    /// <summary>Marca todas las operaciones de tabla que haya ahora en <paramref name="mb"/> (llamar al final del Up generado).</summary>
    public static void Marcar(MigrationBuilder mb)
    {
        foreach (var op in mb.Operations)
            if (Tabla(op) is not null) op.AddAnnotation(Anotacion, true);
    }

    public static void Terminar(MigrationBuilder mb) => mb.Sql($"DROP TABLE IF EXISTS dbo.{TablaDePrevias};");

    public static string? Tabla(MigrationOperation op) => op switch
    {
        CreateTableOperation c => c.Name,
        CreateIndexOperation i => i.Table,
        AddForeignKeyOperation f => f.Table,
        InsertDataOperation d => d.Table,
        _ => null
    };
}
