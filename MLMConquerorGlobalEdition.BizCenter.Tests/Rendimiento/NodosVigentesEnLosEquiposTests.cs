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
/// M3 (tras DQ9 del Plan Q): el nodo vigente es el unico vivo; un borrado no cuenta aunque sea mas reciente.
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
        // Revision del bloque 5 (I1): un miembro pendiente (sin activar) tambien tiene nodo y sale en la rejilla, pero
        // EnrollmentTeamSize no lo cuenta (solo activados no cancelados, como lo deja la cola). El total no puede salir de
        // ahi: tiene que ser el de las filas que se listan.
        ReportesDb.Miembro(db, "AMB-P", Yo, estado: Domain.Entities.Member.MemberAccountStatus.Pending);
        ReportesDb.Nodo(db, "AMB-P", Yo, ReportesDb.Ruta(Yo, "AMB-P"), 2);
        ReportesDb.Puntos(db, Yo, 0).EnrollmentTeamSize = 2;
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Mi_equipo_no_repite_a_quien_tiene_dos_nodos()
    {
        await using var db = await EquipoAsync();

        var equipo = await ClientesDelEquipoTests.Servicio(db).GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 20 });

        equipo.Items.Select(v => v.MemberId).Should().BeEquivalentTo("AMB-A", "AMB-B", "AMB-P");
        equipo.TotalCount.Should().Be(3, "el total es el de las filas listadas, tambien la del pendiente");
        equipo.Items.Single(v => v.MemberId == "AMB-B").Level.Should().Be(2, "el nivel es el del nodo vigente");
    }

    /// <summary>
    /// R8 tras DQ9 (revision del bloque 5, minor c). Antes: con dos nodos vivos del mismo miembro, el vigente era el mas
    /// reciente y desempataba el Id. Ahora UX_GenealogyTree_MemberId_Vivo no deja guardar dos vivos (lo prueba
    /// SqlServerNodoDeGenealogiaTests.Un_segundo_nodo_vivo_del_mismo_miembro_falla_con_el_indice_unico) y
    /// 01-nodos-de-inscripcion-repetidos.sql deja los demas borrados: el vigente es el unico vivo, aunque un borrado sea mas
    /// reciente, y el equipo (CurrentUnder) lo cuenta una vez, en el sitio del vivo.
    /// </summary>
    [Fact]
    public async Task El_nodo_vigente_es_el_unico_vivo_aunque_un_borrado_sea_mas_reciente()
    {
        await using var db = ReportesDb.Nueva();
        var raiz = ReportesDb.Nodo(db, "AMB-R", null, "/AMB-R/", 1);
        await db.SaveChangesAsync();
        var vivo = ReportesDb.Nodo(db, "AMB-X", "AMB-R", "/AMB-R/AMB-X/", 2);
        var borrado = ReportesDb.Nodo(db, "AMB-X", "AMB-R", "/AMB-R/AMB-X/", 2);
        borrado.IsDeleted = true;
        await db.SaveChangesAsync();
        vivo.CreationDate = new DateTime(2020, 1, 1);
        borrado.CreationDate = new DateTime(2026, 1, 1);
        await db.SaveChangesAsync();

        (await db.GenealogyTree.AsNoTracking().NodeOfAsync("AMB-X")).Should().Be(vivo.Node);
        (await db.GenealogyTree.AsNoTracking().CurrentUnder(raiz.Node).Where(g => g.MemberId == "AMB-X")
            .Select(g => g.Node).ToListAsync()).Should().Equal(vivo.Node);
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
