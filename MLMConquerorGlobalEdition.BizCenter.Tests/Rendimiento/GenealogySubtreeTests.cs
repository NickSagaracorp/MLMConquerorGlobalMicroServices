using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>Plan M (M16): el equipo por Node, con la regla del nodo más reciente (R8 del Plan J).</summary>
public class GenealogySubtreeTests
{
    [Fact]
    public async Task ReportesDb_da_a_cada_nodo_el_Node_de_su_ruta()
    {
        await using var db = ReportesDb.Nueva();
        var a = ReportesDb.Nodo(db, "AMB-1", null, ReportesDb.Ruta("AMB-1"), 1);
        var b = ReportesDb.Nodo(db, "AMB-2", "AMB-1", ReportesDb.Ruta("AMB-1", "AMB-2"), 2);
        var c = ReportesDb.Nodo(db, "AMB-3", "AMB-1", ReportesDb.Ruta("AMB-1", "AMB-3"), 2);
        await db.SaveChangesAsync();

        b.Node!.GetAncestor(1).Should().Be(a.Node);
        c.Node!.GetAncestor(1).Should().Be(a.Node);
        b.Node.Should().NotBe(c.Node);
    }

    [Fact]
    public async Task El_equipo_incluye_al_que_mira_y_descarta_los_nodos_obsoletos()
    {
        await using var db = ReportesDb.Nueva();
        var yo = ReportesDb.Nodo(db, "AMB-1", null, ReportesDb.Ruta("AMB-1"), 1);
        ReportesDb.Nodo(db, "AMB-2", "AMB-1", ReportesDb.Ruta("AMB-1", "AMB-2"), 2);
        ReportesDb.Nodo(db, "AMB-9", null, ReportesDb.Ruta("AMB-9"), 1);
        await db.SaveChangesAsync();
        // AMB-3 estuvo bajo AMB-1 (nodo antiguo) y ahora cuelga de AMB-9 (nodo más reciente).
        await ReportesDb.NodoObsoletoAsync(db, "AMB-3", "AMB-1", ReportesDb.Ruta("AMB-1", "AMB-3"), 2);
        ReportesDb.Nodo(db, "AMB-3", "AMB-9", ReportesDb.Ruta("AMB-9", "AMB-3"), 2);
        await db.SaveChangesAsync();

        var equipo = await db.GenealogyTree.AsNoTracking().CurrentUnder(yo.Node!).Select(g => g.MemberId).ToListAsync();

        equipo.Should().BeEquivalentTo("AMB-1", "AMB-2");
    }

    [Fact]
    public async Task El_nodo_de_un_miembro_es_el_mas_reciente()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.Nodo(db, "AMB-1", null, ReportesDb.Ruta("AMB-1"), 1);
        ReportesDb.Nodo(db, "AMB-9", null, ReportesDb.Ruta("AMB-9"), 1);
        await db.SaveChangesAsync();
        await ReportesDb.NodoObsoletoAsync(db, "AMB-3", "AMB-1", ReportesDb.Ruta("AMB-1", "AMB-3"), 2);
        var actual = ReportesDb.Nodo(db, "AMB-3", "AMB-9", ReportesDb.Ruta("AMB-9", "AMB-3"), 2);
        await db.SaveChangesAsync();

        (await db.GenealogyTree.AsNoTracking().NodeOfAsync("AMB-3")).Should().Be(actual.Node);
        (await db.GenealogyTree.AsNoTracking().NodeOfAsync("NADIE")).Should().BeNull();
    }
}
