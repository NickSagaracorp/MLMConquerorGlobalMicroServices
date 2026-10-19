using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Plan P, Tarea 19, Paso 3 («Plan B»): con 1.000.000 de nodos, contar el rango de la clave de Mi equipo tarda 4 s
/// (medido en la base de escala). Sin busqueda ni filtros, el total es el equipo entero, que el nodo ya guarda en sus
/// contadores de pierna (LeftLegCount + RightLegCount, exactos por construccion): no se cuenta. Con busqueda o filtros
/// se sigue contando el rango.
/// </summary>
public class MiEquipoDualTotalPorContadoresTests
{
    private const string Yo = "AMB-000001";
    private const string A  = "AMB-000002";
    private const string B  = "AMB-000003";

    private static async Task<Repository.Context.AppDbContext> EquipoAsync(int izquierda, int derecha)
    {
        var db = ReportesDb.Nueva();
        ReportesDb.Niveles(db);
        ReportesDb.Miembro(db, Yo, alta: new DateTime(2025, 1, 1));
        ReportesDb.Miembro(db, A, Yo, alta: new DateTime(2025, 2, 1));
        ReportesDb.Miembro(db, B, Yo, alta: new DateTime(2025, 3, 1));
        ReportesDb.NodoBinario(db, Yo, null, ReportesDb.Ruta(Yo), TreeSide.Left);
        ReportesDb.NodoBinario(db, A, Yo, ReportesDb.Ruta(Yo, A), TreeSide.Left);
        ReportesDb.NodoBinario(db, B, Yo, ReportesDb.Ruta(Yo, B), TreeSide.Right);
        await db.SaveChangesAsync();
        var yo = await db.DualTeamTree.SingleAsync(d => d.MemberId == Yo);
        yo.LeftLegCount = izquierda;
        yo.RightLegCount = derecha;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    private static DualTeamService Servicio(Repository.Context.AppDbContext db) =>
        new(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));

    [Fact]
    public async Task Sin_busqueda_ni_filtros_el_total_sale_de_los_contadores_del_nodo()
    {
        // Contadores a proposito distintos del rango (2 nodos) para ver de donde sale el total.
        await using var db = await EquipoAsync(izquierda: 40, derecha: 2);

        var r = await Servicio(db).GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 20 });

        r.TotalCount.Should().Be(42);
        r.Items.Select(i => i.MemberId).Should().BeEquivalentTo(new[] { A, B });
    }

    [Fact]
    public async Task Con_busqueda_se_cuenta_el_rango()
    {
        await using var db = await EquipoAsync(izquierda: 40, derecha: 2);

        var r = await Servicio(db).GetMyTeamGridAsync(Yo, new GridDataRequest { PageSize = 20, Search = B });

        r.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Con_un_filtro_se_cuenta_el_rango()
    {
        await using var db = await EquipoAsync(izquierda: 40, derecha: 2);

        var r = await Servicio(db).GetMyTeamGridAsync(Yo, new GridDataRequest
        {
            PageSize = 20,
            Filters = { new GridFilter { Field = "MemberId", Operator = "equal", Value = A } }
        });

        r.TotalCount.Should().Be(1);
    }
}
