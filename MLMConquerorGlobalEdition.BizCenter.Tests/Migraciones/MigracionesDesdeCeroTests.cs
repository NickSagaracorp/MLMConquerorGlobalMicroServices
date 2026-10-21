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
            Skip = "Define MLM_MIGRACIONES_DESDE_CERO=1: crea y borra MLMConquerorGlobalEditionDb_DesdeCero, _Modelo, _PruebaMigraciones_Script y _PruebaMigraciones_Modelo en la instancia local.";
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
    private static readonly string[] Permitidas =
        { DesdeCero, Modelo, "MLMConquerorGlobalEditionDb_PruebaMigraciones_Script", "MLMConquerorGlobalEditionDb_PruebaMigraciones_Modelo" };

    private const string IgualQueLaLocal =
        "deriva anterior al Plan Q, igual en la base local (comparada en solo lectura en el Bloque 6): ";

    /// <summary>
    /// Diferencias conocidas entre lo que crean las migraciones y el modelo, cada una con su motivo. Todas estan igual en la
    /// base local: corregirlas cambia las bases reales (estrechar columnas con datos, rowversion) y queda fuera del Plan Q.
    /// La clave es la linea de <see cref="EsquemaAsync"/> del lado de las migraciones o del modelo.
    /// </summary>
    internal static readonly Dictionary<string, string> Excepciones = new()
    {
        ["MemberProfiles.DefaultLanguage:nvarchar:8:NO"] = IgualQueLaLocal + "AddProfilePreferencesAndHistories la crea nvarchar(8); el modelo no le da longitud",
        ["MemberProfiles.DefaultLanguage:nvarchar:-1:NO"] = IgualQueLaLocal + "la otra cara de la anterior",
        ["TicketCategories.Name:nvarchar:-1:NO"] = IgualQueLaLocal + "la crea InitialCreate; HelpdeskModule (200) no la toca porque la tabla ya existia",
        ["TicketCategories.Name:nvarchar:200:NO"] = IgualQueLaLocal + "la otra cara de la anterior",
        ["TicketComments.AuthorType:nvarchar:-1:NO"] = IgualQueLaLocal + "AddTicketCommentAuthorTypeColumn la crea nvarchar(max); el modelo dice 20",
        ["TicketComments.AuthorType:nvarchar:20:NO"] = IgualQueLaLocal + "la otra cara de la anterior",
        ["TicketComments ix IX_TicketComments_TicketId:0:"] = IgualQueLaLocal + "indice de InitialCreate; HelpdeskModule lo sustituye en el modelo por los dos siguientes",
        ["TicketComments ix IX_TicketComments_IsInternal:0:"] = IgualQueLaLocal + "de HelpdeskModule sobre una tabla que ya existia",
        ["TicketComments ix IX_TicketComments_TicketId_CreationDate:0:"] = IgualQueLaLocal + "de HelpdeskModule sobre una tabla que ya existia",
        ["Tickets ix IX_Tickets_CategoryId_Status:0:"] = IgualQueLaLocal + "indice de InitialCreate que el modelo ya no tiene",
        ["Tickets ix IX_Tickets_Status_CreationDate:0:"] = IgualQueLaLocal + "indice de InitialCreate que el modelo ya no tiene",
        ["Tickets ix IX_Tickets_AssignedToUserId_Status:0:"] = IgualQueLaLocal + "de HelpdeskModule sobre una tabla que ya existia",
        ["Tickets ix IX_Tickets_MemberId_CreationDate:0:"] = IgualQueLaLocal + "de HelpdeskModule sobre una tabla que ya existia",
        ["Tickets ix IX_Tickets_Status_LastUpdateDate:0:"] = IgualQueLaLocal + "de HelpdeskModule sobre una tabla que ya existia",
        ["Tickets.AssignedToUserId:nvarchar:-1:YES"] = IgualQueLaLocal + "Tickets la crea InitialCreate y la completa RepairTicketsTableSchema; el modelo dice 450",
        ["Tickets.AssignedToUserId:nvarchar:450:YES"] = IgualQueLaLocal + "la otra cara de la anterior",
        ["Tickets.MemberId:nvarchar:-1:NO"] = IgualQueLaLocal + "como AssignedToUserId; el modelo dice 450",
        ["Tickets.MemberId:nvarchar:450:NO"] = IgualQueLaLocal + "la otra cara de la anterior",
        ["Tickets.MergedIntoTicketId:nvarchar:-1:YES"] = IgualQueLaLocal + "como AssignedToUserId; el modelo dice 36",
        ["Tickets.MergedIntoTicketId:nvarchar:36:YES"] = IgualQueLaLocal + "la otra cara de la anterior",
        ["Tickets.RowVersion:varbinary:-1:YES"] = IgualQueLaLocal + "RepairTicketsTableSchema la crea varbinary(max); el modelo dice rowversion",
        ["Tickets.RowVersion:timestamp::YES"] = IgualQueLaLocal + "la otra cara de la anterior",
        ["Tickets.Subject:nvarchar:-1:NO"] = IgualQueLaLocal + "como AssignedToUserId; el modelo dice 300",
        ["Tickets.Subject:nvarchar:300:NO"] = IgualQueLaLocal + "la otra cara de la anterior",
    };

    internal static string Cadena(string bd) =>
        $"Server=.;Database={bd};Integrated Security=true;TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=true;";

    internal static AppDbContext Contexto(string bd) =>
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
            var deMas = migrada.Except(modelo).ToList();
            var deMenos = modelo.Except(migrada).ToList();
            deMas.Where(d => !Excepciones.ContainsKey(d)).Order().Should()
                .BeEmpty("lo que las migraciones crean de mas o distinto del modelo");
            deMenos.Where(d => !Excepciones.ContainsKey(d)).Order().Should()
                .BeEmpty("lo que el modelo tiene y las migraciones no crean");
            Excepciones.Keys.Where(k => !deMas.Contains(k) && !deMenos.Contains(k)).Order().Should()
                .BeEmpty("una excepcion que ya no hace falta se quita de la lista");
            (await ObjetosAsync(DesdeCero)).Should().Contain(new[]
                { "DualChildKey", "Numeros", "DualAncestorKeys", "GenealogyAncestors", "DualTeamRootOrdinal" });
        }
        finally
        {
            await BorrarAsync(DesdeCero);
            await BorrarAsync(Modelo);
        }
    }

    /// <summary>
    /// Columnas (tipo, longitud, nulos), indices (unico, filtro), claves foraneas (columnas, tabla referida y accion al
    /// borrar) y restricciones CHECK (definicion) de las tablas de usuario, sin el historial.
    /// </summary>
    internal static async Task<HashSet<string>> EsquemaAsync(string bd)
    {
        const string sql = @"
SELECT CONCAT(c.TABLE_NAME, '.', c.COLUMN_NAME, ':', c.DATA_TYPE, ':', c.CHARACTER_MAXIMUM_LENGTH, ':', c.IS_NULLABLE)
FROM INFORMATION_SCHEMA.COLUMNS c WHERE c.TABLE_NAME <> '__EFMigrationsHistory'
UNION ALL
SELECT CONCAT(t.name, ' ix ', i.name, ':', i.is_unique, ':', i.filter_definition)
FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
WHERE i.name IS NOT NULL AND t.name <> '__EFMigrationsHistory'
UNION ALL
SELECT CONCAT(OBJECT_NAME(fk.parent_object_id), ' fk ', fk.name, ':',
    (SELECT STRING_AGG(CONCAT(COL_NAME(fc.parent_object_id, fc.parent_column_id), '>', COL_NAME(fc.referenced_object_id, fc.referenced_column_id)), ',')
            WITHIN GROUP (ORDER BY fc.constraint_column_id)
     FROM sys.foreign_key_columns fc WHERE fc.constraint_object_id = fk.object_id),
    ':', OBJECT_NAME(fk.referenced_object_id), ':', fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT)
FROM sys.foreign_keys fk
UNION ALL
SELECT CONCAT(OBJECT_NAME(ck.parent_object_id), ' ck ', ck.name, ':', ck.definition COLLATE DATABASE_DEFAULT)
FROM sys.check_constraints ck;";
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

    internal static async Task BorrarAsync(string bd)
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
