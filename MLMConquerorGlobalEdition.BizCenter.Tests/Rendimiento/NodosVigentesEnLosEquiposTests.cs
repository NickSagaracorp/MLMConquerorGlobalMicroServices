using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Revisión de las Tareas 6–13 del Plan M:
/// I5: Mi equipo y los puntos de equipo cuentan solo los nodos VIGENTES (CurrentUnder): un miembro con
///     dos nodos (R8 del Plan J) sale una vez.
/// M3: el nodo vigente se desempata por Id cuando dos nodos tienen la misma fecha.
/// C1: sin Node no hay dato (null), nunca un 0 que se persistiría como si el equipo no tuviera puntos.
/// </summary>
public class NodosVigentesEnLosEquiposTests
{
    private const string Yo = "AMB-000001";

    /// <summary>YO ── A, y B con dos nodos bajo YO (uno obsoleto, otro vigente).</summary>
    private static async Task<Repository.Context.AppDbContext> EquipoAsync()
    {
        var db = ReportesDb.Nueva();
        ReportesDb.Niveles(db);
        foreach (var id in new[] { Yo, "AMB-A", "AMB-B" })
            ReportesDb.Miembro(db, id, id == Yo ? null : Yo);
        ReportesDb.Nodo(db, Yo, null, ReportesDb.Ruta(Yo), 1);
        await db.SaveChangesAsync();
        ReportesDb.Nodo(db, "AMB-A", Yo, ReportesDb.Ruta(Yo, "AMB-A"), 2);
        await ReportesDb.NodoObsoletoAsync(db, "AMB-B", Yo, ReportesDb.Ruta(Yo, "AMB-B"), 2);
        ReportesDb.Nodo(db, "AMB-B", "AMB-A", ReportesDb.Ruta(Yo, "AMB-A", "AMB-B"), 3);
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Mi_equipo_no_repite_a_quien_tiene_dos_nodos()
    {
        await using var db = await EquipoAsync();

        var equipo = await ClientesDelEquipoTests.Servicio(db).GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 20 });

        equipo.Items.Select(v => v.MemberId).Should().BeEquivalentTo("AMB-A", "AMB-B");
        equipo.TotalCount.Should().Be(2);
        equipo.Items.Single(v => v.MemberId == "AMB-B").Level.Should().Be(2, "el nivel es el del nodo vigente");
    }

    [Fact]
    public async Task El_nodo_vigente_desempata_por_Id()
    {
        await using var db = ReportesDb.Nueva();
        var a = ReportesDb.Nodo(db, "AMB-X", null, "/AMB-X/", 1);
        var b = ReportesDb.Nodo(db, "AMB-X", null, "/AMB-X/", 1);
        await db.SaveChangesAsync();
        var mismaFecha = new DateTime(2026, 1, 1);
        a.CreationDate = b.CreationDate = mismaFecha;
        await db.SaveChangesAsync();
        var ganador = string.CompareOrdinal(a.Id, b.Id) > 0 ? a : b;

        (await db.GenealogyTree.AsNoTracking().NodeOfAsync("AMB-X")).Should().Be(ganador.Node);
        (await db.GenealogyTree.AsNoTracking().CurrentUnder(HierarchyId.GetRoot()).Where(g => g.MemberId == "AMB-X").CountAsync())
            .Should().Be(1);
    }

    [Fact]
    public async Task Sin_Node_los_puntos_de_equipo_no_tienen_dato_en_vez_de_cero()
    {
        await using var db = ReportesDb.Nueva();
        ReportesDb.Nodo(db, "AMB-SIN", null, "/AMB-SIN/", 1).Node = null;
        await db.SaveChangesAsync();

        (await new EnrollmentTeamPointsService(db).RecomputeEnrollmentPointsAsync("AMB-SIN")).Should().BeNull();
        (await new EnrollmentTeamPointsService(db).RecomputeEnrollmentPointsAsync("NADIE")).Should().BeNull();
    }
}
