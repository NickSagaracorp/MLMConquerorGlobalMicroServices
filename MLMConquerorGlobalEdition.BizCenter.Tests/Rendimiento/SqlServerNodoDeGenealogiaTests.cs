using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>Plan M, Bloque 2: esquema, relleno y coherencia de GenealogyTree.Node en la base local.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerNodoDeGenealogiaTests
{
    [SqlServerFact]
    public async Task Node_es_hierarchyid_con_sus_dos_indices()
    {
        await using var db = SqlServerReportes.Db();

        var tipo = await db.Database.SqlQuery<string>($"""
            SELECT TYPE_NAME(c.user_type_id) AS [Value] FROM sys.columns c
            WHERE c.object_id = OBJECT_ID('GenealogyTree') AND c.name = 'Node'
            """).SingleAsync();
        var indices = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.indexes
            WHERE object_id = OBJECT_ID('GenealogyTree')
              AND ((name = 'IX_GenealogyTree_Node' AND is_unique = 1 AND has_filter = 0)
                OR name = 'IX_GenealogyTree_NodeLevel_Node')
            """).SingleAsync();

        tipo.Should().Be("hierarchyid");
        indices.Should().Be(2);
    }

    /// <summary>
    /// El índice único hace de filtro «Node no nulo» con NodeNullKey (10619): muchas filas sin Node
    /// conviven, y dos filas con el mismo Node chocan con un error que IsDuplicateNode reconoce (R5).
    /// </summary>
    [SqlServerFact]
    public Task El_indice_admite_muchos_nodos_nulos_y_rechaza_dos_iguales() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        string a = SqlServerReportes.NuevoId("TM"), b = SqlServerReportes.NuevoId("TM"),
               c = SqlServerReportes.NuevoId("TM"), d = SqlServerReportes.NuevoId("TM");

        ReportesDb.Nodo(db, a, null, ReportesDb.Ruta(a), 1).Node = null;
        ReportesDb.Nodo(db, b, null, ReportesDb.Ruta(b), 1).Node = null;
        await db.SaveChangesAsync();

        var libre = await GenealogyNodes.NextChildAsync(db, null);
        ReportesDb.Nodo(db, c, null, ReportesDb.Ruta(c), 1).Node = libre;
        ReportesDb.Nodo(db, d, null, ReportesDb.Ruta(d), 1).Node = libre;
        var choque = await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();

        GenealogyNodes.IsDuplicateNode(choque.Which).Should().BeTrue();
        await tx.RollbackAsync();
    });
}
