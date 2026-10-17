using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>Plan M (M13): el siguiente Node es el hermano siguiente del último hijo del padre.</summary>
public class GenealogyNodesTests
{
    private static void Nodo(AppDbContext db, string id, string? padre, string node)
        => ReportesDb.Nodo(db, id, padre, "/" + id + "/", 1).Node = HierarchyId.Parse(node);

    [Fact]
    public async Task El_primer_hijo_cuelga_del_padre()
    {
        await using var db = ReportesDb.Nueva();
        Nodo(db, "AMB-1", null, "/1/");
        await db.SaveChangesAsync();

        (await GenealogyNodes.NextChildAsync(db, "AMB-1")).Should().Be(HierarchyId.Parse("/1/1/"));
    }

    [Fact]
    public async Task El_siguiente_hijo_va_detras_del_ultimo_aunque_este_borrado()
    {
        await using var db = ReportesDb.Nueva();
        Nodo(db, "AMB-1", null, "/1/");
        Nodo(db, "AMB-2", "AMB-1", "/1/1/");
        Nodo(db, "AMB-3", "AMB-1", "/1/2/");
        await db.SaveChangesAsync();
        var borrado = await db.GenealogyTree.SingleAsync(g => g.MemberId == "AMB-3");
        borrado.IsDeleted = true;
        await db.SaveChangesAsync();

        (await GenealogyNodes.NextChildAsync(db, "AMB-1")).Should().Be(HierarchyId.Parse("/1/3/"),
            "un ordinal no se reutiliza: el índice único no está filtrado por IsDeleted");
    }

    [Fact]
    public async Task Si_el_padre_aun_no_tiene_nodo_devuelve_nulo()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.Nodo(db, "AMB-1", null, "/AMB-1/", 1).Node = null;
        await db.SaveChangesAsync();

        (await GenealogyNodes.NextChildAsync(db, "AMB-1")).Should().BeNull();
    }

    [Fact]
    public async Task Una_raiz_nueva_va_detras_de_la_ultima_raiz()
    {
        await using var db = ReportesDb.Nueva();
        (await GenealogyNodes.NextChildAsync(db, null)).Should().Be(HierarchyId.Parse("/1/"));

        Nodo(db, "AMB-1", null, "/1/");
        Nodo(db, "AMB-2", "AMB-HUERFANO", "/2/");   // padre inexistente: cuelga de la raíz
        Nodo(db, "AMB-3", "AMB-1", "/1/1/");
        await db.SaveChangesAsync();

        (await GenealogyNodes.NextChildAsync(db, null)).Should().Be(HierarchyId.Parse("/3/"));
    }

    [Fact]
    public async Task Con_diez_hijos_o_mas_el_orden_es_por_nodo_y_no_por_texto()
    {
        await using var db = ReportesDb.Nueva();
        Nodo(db, "AMB-1", null, "/1/");
        for (var i = 1; i <= 10; i++)
            Nodo(db, $"AMB-H{i}", "AMB-1", $"/1/{i}/");
        await db.SaveChangesAsync();

        (await GenealogyNodes.NextChildAsync(db, "AMB-1")).Should().Be(HierarchyId.Parse("/1/11/"),
            "como texto, \"/1/9/\" iría detrás de \"/1/10/\"");
    }
}
