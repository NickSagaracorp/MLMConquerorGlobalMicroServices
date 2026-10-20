using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Repository.Configurations;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Plan M (M11, M12, M21): Node es hierarchyid en SQL Server sin que el anfitrión llame a
/// UseHierarchyId, con índice único entre los nodos no nulos e índice en anchura; en InMemory funciona como texto
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

    /// <summary>
    /// SQL Server no admite un índice filtrado sobre una columna CLR (error 10619 con «[Node] IS NOT NULL»),
    /// ni filtros sobre columnas calculadas. La unicidad «solo entre los Node no nulos» se consigue con
    /// una segunda clave: NodeNullKey vale el Id cuando Node es nulo (cada fila sin nodo es distinta) y
    /// NULL cuando no lo es, así que dos filas con el mismo Node chocan como (Node, NULL).
    /// </summary>
    [Fact]
    public void Indice_unico_en_profundidad_entre_los_nodos_no_nulos_y_en_anchura_por_nivel()
    {
        using var db = SqlServerSinUseHierarchyId();
        var tipo = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(GenealogyEntity))!;

        var profundidad = tipo.GetIndexes().Single(i => i.GetDatabaseName() == "IX_GenealogyTree_Node");
        profundidad.Properties.Select(p => p.Name).Should().Equal("Node", "NodeNullKey");
        profundidad.IsUnique.Should().BeTrue();
        profundidad.GetFilter().Should().BeNull("un filtro sobre una columna CLR no se puede crear (10619)");
        // CurrentUnder lee además CreationDate (nodo vigente, R8) e IsDeleted (filtro global): sin ellas el
        // optimizador prefiere recorrer la tabla a hacer un seek con búsquedas en la clave agrupada.
        profundidad.GetIncludeProperties().Should().BeEquivalentTo("MemberId", "Level", "CreationDate", "IsDeleted");
        tipo.FindProperty("NodeNullKey")!.GetComputedColumnSql()
            .Should().Be("CASE WHEN [Node] IS NULL THEN CAST([Id] AS nvarchar(100)) END"); // revisión M8
        tipo.FindProperty("NodeNullKey")!.GetIsStored().Should().BeTrue();

        // Revisión M5: los hermanos de un nodo se buscan por ParentMemberId y se leen por Node.
        tipo.GetIndexes().Single(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "ParentMemberId" }))
            .GetIncludeProperties().Should().BeEquivalentTo("Node");
        // Revisión M4: GenealogySubtree.Under es exacto solo con ordinales enteros.
        tipo.GetCheckConstraints().Should().ContainSingle(c => c.Name == "CK_GenealogyTree_Node_OrdinalesEnteros"
            && c.Sql == "[Node] IS NULL OR CHARINDEX('.', [Node].ToString()) = 0");

        tipo.FindProperty("NodeLevel")!.GetComputedColumnSql().Should().Be("[Node].GetLevel()");
        tipo.GetIndexes().Should().Contain(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "NodeLevel", "Node" }));
    }

    /// <summary>Plan Q (DQ9): un solo nodo vivo por miembro.</summary>
    [Fact]
    public void Indice_unico_de_un_nodo_vivo_por_miembro()
    {
        using var db = SqlServerSinUseHierarchyId();
        var tipo = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(GenealogyEntity))!;

        var vivo = tipo.GetIndexes().Single(i => i.GetDatabaseName() == GenealogyConfiguration.LiveMemberIndexName);
        vivo.Properties.Select(p => p.Name).Should().Equal("MemberId");
        vivo.IsUnique.Should().BeTrue();
        vivo.GetFilter().Should().Be("[IsDeleted] = 0");
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
