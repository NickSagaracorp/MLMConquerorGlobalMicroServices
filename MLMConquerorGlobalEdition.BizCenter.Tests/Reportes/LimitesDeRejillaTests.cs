using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>Plan Q (DQ7, P7 del Plan P): por encima de TeamGridLimits.FreeMaxRows, Mi equipo ordena por estructura y lo avisa.</summary>
public class LimitesDeRejillaTests
{
    private const string Yo = "AMB-000001";
    private const string A  = "AMB-000002";   // «Nombre AMB-000002»: el primero por estructura, el segundo por nombre desc
    private const string B  = "AMB-000003";

    private static GridDataRequest PorNombreDesc() =>
        new() { PageSize = 20, Sorts = { new GridSort { Field = "FullName", Direction = "desc" } } };

    private static async Task<AppDbContext> DualAsync(int tamano)
    {
        var db = ReportesDb.Nueva();
        ReportesDb.Niveles(db);
        foreach (var id in new[] { Yo, A, B }) ReportesDb.Miembro(db, id, id == Yo ? null : Yo);
        ReportesDb.NodoBinario(db, Yo, null, ReportesDb.Ruta(Yo), TreeSide.Left);
        ReportesDb.NodoBinario(db, A, Yo, ReportesDb.Ruta(Yo, A), TreeSide.Left);
        ReportesDb.NodoBinario(db, B, Yo, ReportesDb.Ruta(Yo, B), TreeSide.Right);
        await db.SaveChangesAsync();
        var yo = await db.DualTeamTree.SingleAsync(d => d.MemberId == Yo);
        yo.LeftLegCount = tamano - 1;
        yo.RightLegCount = 1;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    private static async Task<AppDbContext> InscripcionAsync(int tamano)
    {
        var db = ReportesDb.Nueva();
        ReportesDb.Niveles(db);
        foreach (var id in new[] { Yo, A, B }) ReportesDb.Miembro(db, id, id == Yo ? null : Yo);
        ReportesDb.Nodo(db, Yo, null, ReportesDb.Ruta(Yo), 1);
        ReportesDb.Nodo(db, A, Yo, ReportesDb.Ruta(Yo, A), 2);   // Node /r/1/
        ReportesDb.Nodo(db, B, Yo, ReportesDb.Ruta(Yo, B), 2);   // Node /r/2/
        ReportesDb.Puntos(db, Yo, 0).EnrollmentTeamSize = tamano;
        await db.SaveChangesAsync();
        return db;
    }

    private static DualTeamService Dual(AppDbContext db) =>
        new(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));

    private static EnrollmentTeamService Inscripcion(AppDbContext db) =>
        new(db, CalificadoEnLosEquiposTests.Rangos(db), new EnrollmentTeamPointsService(db), CalificadoEnLosEquiposTests.Calificacion(db));

    [Fact]
    public async Task Dual_con_mas_de_20000_el_orden_pedido_pasa_a_estructura_y_se_avisa()
    {
        await using var db = await DualAsync(30_000);
        var r = await Dual(db).GetMyTeamGridAsync(Yo, PorNombreDesc());
        r.Limited.Should().BeTrue();
        r.Items.Select(i => i.MemberId).Should().Equal(A, B);
    }

    [Fact]
    public async Task Dual_con_20000_o_menos_se_respeta_el_orden()
    {
        await using var db = await DualAsync(2);
        var r = await Dual(db).GetMyTeamGridAsync(Yo, PorNombreDesc());
        r.Limited.Should().BeFalse();
        r.Items.Select(i => i.MemberId).Should().Equal(B, A);
    }

    [Fact]
    public async Task Dual_sin_orden_pedido_ordena_por_estructura()
    {
        await using var db = await DualAsync(2);
        var r = await Dual(db).GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 20 });
        r.Items.Select(i => i.MemberId).Should().Equal(A, B);
    }

    [Fact]
    public async Task Inscripcion_con_mas_de_20000_el_orden_pedido_pasa_a_estructura_y_se_avisa()
    {
        await using var db = await InscripcionAsync(30_000);
        var r = await Inscripcion(db).GetMyTeamGridAsync(Yo, PorNombreDesc());
        r.Limited.Should().BeTrue();
        r.Items.Select(i => i.MemberId).Should().Equal(A, B);
    }

    [Fact]
    public async Task Inscripcion_con_20000_o_menos_se_respeta_el_orden()
    {
        await using var db = await InscripcionAsync(2);
        var r = await Inscripcion(db).GetMyTeamGridAsync(Yo, PorNombreDesc());
        r.Limited.Should().BeFalse();
        r.Items.Select(i => i.MemberId).Should().Equal(B, A);
    }

    [Fact]
    public async Task Inscripcion_sin_orden_pedido_ordena_por_estructura()
    {
        await using var db = await InscripcionAsync(2);
        var r = await Inscripcion(db).GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 20 });
        r.Items.Select(i => i.MemberId).Should().Equal(A, B);
    }

    /// <summary>Plan Q (DQ14): sin filtros el total es el contador guardado; con busqueda se cuenta el resultado.</summary>
    [Fact]
    public async Task Inscripcion_el_total_sin_filtros_sale_de_EnrollmentTeamSize()
    {
        await using var db = await InscripcionAsync(42);
        (await Inscripcion(db).GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 20 })).TotalCount.Should().Be(42);
        (await Inscripcion(db).GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 20, Search = "AMB-000003" }))
            .TotalCount.Should().Be(1, "con busqueda se cuenta el resultado");
    }

    /// <summary>Plan Q (DQ14): las lecturas sin rejilla tampoco cuentan el equipo sin filtros.</summary>
    [Fact]
    public async Task Las_lecturas_sin_rejilla_toman_el_total_sin_filtros_de_los_contadores()
    {
        await using (var db = await InscripcionAsync(42))
        {
            (await Inscripcion(db).GetMyTeamAsync(Yo, 1, 20, null, null, null)).TotalCount.Should().Be(42);
            (await Inscripcion(db).GetMyTeamAsync(Yo, 1, 20, "AMB-000003", null, null)).TotalCount.Should().Be(1);
        }
        await using (var db = await DualAsync(42))
        {
            (await Dual(db).GetMyTeamAsync(Yo, 1, 20, null, null, null)).TotalCount.Should().Be(42);
            (await Dual(db).GetMyTeamAsync(Yo, 1, 20, "AMB-000003", null, null)).TotalCount.Should().Be(1);
        }
    }

    /// <summary>Plan Q (DQ8): por encima del limite, la busqueda es por el comienzo del nombre o del id.</summary>
    [Fact]
    public async Task Dual_con_mas_de_20000_busca_por_prefijo_y_no_por_subcadena()
    {
        await using var db = await DualAsync(30_000);
        (await Dual(db).GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 20, Search = "000003" })).Items.Should().BeEmpty();
        (await Dual(db).GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 20, Search = "amb-000003" }))
            .Items.Select(i => i.MemberId).Should().Equal(B);
    }

    [Fact]
    public async Task Inscripcion_con_mas_de_20000_busca_por_prefijo_y_no_por_subcadena()
    {
        await using var db = await InscripcionAsync(30_000);
        (await Inscripcion(db).GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 20, Search = "000003" })).Items.Should().BeEmpty();
        (await Inscripcion(db).GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 20, Search = "amb-000003" }))
            .Items.Select(i => i.MemberId).Should().Equal(B);
    }
}
