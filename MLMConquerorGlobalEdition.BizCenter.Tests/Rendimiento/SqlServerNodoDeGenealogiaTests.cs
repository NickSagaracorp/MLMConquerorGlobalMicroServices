using System.Data;
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

    /// <summary>Condición de despliegue del Bloque 3 (R17): ninguna fila sin Node y todas coherentes.</summary>
    internal const string Coherencia = """
        SELECT
          (SELECT COUNT(*) FROM GenealogyTree WHERE Node IS NULL) AS SinNode,
          (SELECT COUNT(*) FROM GenealogyTree c
           OUTER APPLY (SELECT TOP (1) p.Node FROM GenealogyTree p
                        WHERE p.MemberId = c.ParentMemberId ORDER BY p.CreationDate DESC) pp
           WHERE c.Node IS NOT NULL
             AND ((pp.Node IS NULL AND c.Node.GetLevel() <> 1)
               OR (pp.Node IS NOT NULL AND c.Node.GetAncestor(1) <> pp.Node))) AS Incoherentes
        """;

    private sealed class Resultado { public int SinNode { get; set; } public int Incoherentes { get; set; } }

    [SqlServerFact]
    public async Task Todas_las_filas_tienen_un_Node_coherente_con_su_padre()
    {
        await using var db = SqlServerReportes.Db();
        var r = await db.Database.SqlQueryRaw<Resultado>(Coherencia).SingleAsync();

        r.SinNode.Should().Be(0);
        r.Incoherentes.Should().Be(0);
    }

    [SqlServerFact]
    public Task El_equipo_por_Node_es_el_mismo_que_por_la_ruta() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        var muestra = await db.GenealogyTree.AsNoTracking()
            .Where(g => g.Level >= 1 && g.Level <= 4 && g.Node != null)
            .OrderBy(g => g.MemberId)
            .Select(g => new { g.HierarchyPath, g.Node })
            .Take(20)
            .ToListAsync();

        foreach (var m in muestra)
        {
            var prefijo = TreePrefix.Of(m.HierarchyPath);
            var porRuta = await db.GenealogyTree.CountAsync(g => g.HierarchyPath.StartsWith(prefijo));
            var porNodo = await db.GenealogyTree.CountAsync(g => g.Node!.IsDescendantOf(m.Node));
            porNodo.Should().Be(porRuta, m.HierarchyPath);
        }
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Un_alta_nueva_cuelga_de_su_patrocinador_en_SQL() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var patrocinador = await db.GenealogyTree.AsNoTracking()
            .Where(g => g.Node != null && g.Level == 2).OrderBy(g => g.MemberId)
            .Select(g => new { g.MemberId, g.Node }).FirstAsync();

        var nuevo = await GenealogyNodes.NextChildAsync(db, patrocinador.MemberId);
        var otraRaiz = await GenealogyNodes.NextChildAsync(db, null);

        nuevo!.GetAncestor(1).Should().Be(patrocinador.Node);
        (await db.GenealogyTree.IgnoreQueryFilters().AnyAsync(g => g.Node == nuevo)).Should().BeFalse();
        otraRaiz!.GetLevel().Should().Be(1);
        (await db.GenealogyTree.IgnoreQueryFilters().AnyAsync(g => g.Node == otraRaiz)).Should().BeFalse();
        await tx.RollbackAsync();
    });
}
