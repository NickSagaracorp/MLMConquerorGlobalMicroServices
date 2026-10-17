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

    /// <summary>Revisión I4: fallar cerrado. Un hijo de un padre sin Node no aparecería en ningún equipo.</summary>
    [Fact]
    public async Task Si_el_padre_tiene_nodos_pero_ninguno_con_Node_lanza_en_vez_de_crear_un_nodo_invisible()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.Nodo(db, "AMB-1", null, "/AMB-1/", 1).Node = null;
        await db.SaveChangesAsync();

        await FluentActions.Awaiting(() => GenealogyNodes.NextChildAsync(db, "AMB-1"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*AMB-1*");
    }

    [Fact]
    public async Task Un_padre_que_no_esta_en_el_arbol_da_una_raiz_nueva_como_su_ruta()
    {
        await using var db = ReportesDb.Nueva();
        Nodo(db, "AMB-1", null, "/1/");
        await db.SaveChangesAsync();

        (await GenealogyNodes.NextChildAsync(db, "NADIE")).Should().Be(HierarchyId.Parse("/2/"));
    }

    /// <summary>Revisión M1: el padre se busca entre los nodos vivos; los hermanos, también entre los borrados.</summary>
    [Fact]
    public async Task Un_nodo_borrado_del_padre_no_es_su_nodo()
    {
        await using var db = ReportesDb.Nueva();
        Nodo(db, "AMB-1", null, "/1/");
        Nodo(db, "AMB-1", null, "/2/");
        await db.SaveChangesAsync();
        var reciente = await db.GenealogyTree.SingleAsync(g => g.Node == HierarchyId.Parse("/2/"));
        reciente.IsDeleted    = true;
        reciente.CreationDate = reciente.CreationDate.AddDays(1); // el más reciente, pero borrado
        await db.SaveChangesAsync();

        (await GenealogyNodes.NextChildAsync(db, "AMB-1")).Should().Be(HierarchyId.Parse("/1/1/"));
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
