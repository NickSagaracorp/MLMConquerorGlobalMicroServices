using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Plan M (M11, M12, M21): Node es hierarchyid en SQL Server sin que el anfitrión llame a
/// UseHierarchyId, con índice único filtrado e índice en anchura; en InMemory funciona como texto
/// y IsDescendantOf se evalúa en memoria (R11).
/// </summary>
public class NodoDeGenealogiaModeloTests
{
    // Solo construye el modelo: no abre conexión.
    private static AppDbContext SqlServerSinUseHierarchyId() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=.;Database=NoSeAbre;").Options);

    [Fact]
    public void En_SQL_Server_Node_es_hierarchyid_aunque_el_anfitrion_no_lo_pida()
    {
        using var db = SqlServerSinUseHierarchyId();
        db.Model.FindEntityType(typeof(GenealogyEntity))!.FindProperty(nameof(GenealogyEntity.Node))!
            .GetRelationalTypeMapping().StoreType.Should().Be("hierarchyid");
    }

    [Fact]
    public void Indice_unico_filtrado_en_profundidad_y_en_anchura_por_nivel()
    {
        using var db = SqlServerSinUseHierarchyId();
        var tipo = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(GenealogyEntity))!;

        var profundidad = tipo.GetIndexes().Single(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "Node" }));
        profundidad.IsUnique.Should().BeTrue();
        profundidad.GetFilter().Should().Be("[Node] IS NOT NULL");
        profundidad.GetDatabaseName().Should().Be("IX_GenealogyTree_Node");
        profundidad.GetIncludeProperties().Should().BeEquivalentTo("MemberId", "Level");

        tipo.FindProperty("NodeLevel")!.GetComputedColumnSql().Should().Be("[Node].GetLevel()");
        tipo.GetIndexes().Should().Contain(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "NodeLevel", "Node" }));
    }

    [Fact]
    public async Task En_InMemory_se_guarda_y_se_filtra_por_descendencia()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.Nodo(db, "AMB-1", null, "/AMB-1/", 1).Node = HierarchyId.Parse("/1/");
        ReportesDb.Nodo(db, "AMB-2", "AMB-1", "/AMB-1/AMB-2/", 2).Node = HierarchyId.Parse("/1/1/");
        ReportesDb.Nodo(db, "AMB-3", null, "/AMB-3/", 1).Node = HierarchyId.Parse("/2/");
        await db.SaveChangesAsync();
        var raiz = HierarchyId.Parse("/1/");

        var equipo = await db.GenealogyTree.Where(g => g.Node!.IsDescendantOf(raiz)).Select(g => g.MemberId).ToListAsync();

        equipo.Should().BeEquivalentTo("AMB-1", "AMB-2");
    }
}
