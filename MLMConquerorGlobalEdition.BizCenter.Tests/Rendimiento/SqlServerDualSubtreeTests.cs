using System.Data;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using Xunit.Abstractions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>Plan M (M17–M20): la CTE del binario, con árboles propios dentro de una transacción.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerDualSubtreeTests
{
    private readonly ITestOutputHelper _salida;

    public SqlServerDualSubtreeTests(ITestOutputHelper salida) => _salida = salida;

    [SqlServerFact]
    public Task Devuelve_cada_descendiente_con_su_profundidad_y_su_pierna() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = SqlServerReportes.NuevoId(); var a = SqlServerReportes.NuevoId(); var b = SqlServerReportes.NuevoId(); var c = SqlServerReportes.NuevoId();
        ReportesDb.NodoBinario(db, r, null, ReportesDb.Ruta(r), TreeSide.Left);
        ReportesDb.NodoBinario(db, a, r, ReportesDb.Ruta(r, a), TreeSide.Left);
        ReportesDb.NodoBinario(db, b, r, ReportesDb.Ruta(r, b), TreeSide.Right);
        ReportesDb.NodoBinario(db, c, a, ReportesDb.Ruta(r, a, c), TreeSide.Right);
        await db.SaveChangesAsync();

        var filas = await DualSubtree.DescendantsAsync(db, r, 100);

        filas!.Select(f => (f.MemberId, f.Depth, (TreeSide)f.Leg)).Should().BeEquivalentTo(new[]
        {
            (a, 1, TreeSide.Left), (b, 1, TreeSide.Right), (c, 2, TreeSide.Left)
        });
        await tx.RollbackAsync();
    });

    /// <summary>Revisión: los reportes binarios reducen primero por pierna (D4): se baja solo por un hijo.</summary>
    [SqlServerFact]
    public Task Con_pierna_solo_baja_por_ese_lado() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = SqlServerReportes.NuevoId(); var a = SqlServerReportes.NuevoId(); var b = SqlServerReportes.NuevoId(); var c = SqlServerReportes.NuevoId();
        ReportesDb.NodoBinario(db, r, null, ReportesDb.Ruta(r), TreeSide.Left);
        ReportesDb.NodoBinario(db, a, r, ReportesDb.Ruta(r, a), TreeSide.Left);
        ReportesDb.NodoBinario(db, b, r, ReportesDb.Ruta(r, b), TreeSide.Right);
        ReportesDb.NodoBinario(db, c, a, ReportesDb.Ruta(r, a, c), TreeSide.Right);
        await db.SaveChangesAsync();

        (await DualSubtree.DescendantsAsync(db, r, 100, TreeSide.Left))!.Select(f => f.MemberId).Should().BeEquivalentTo(a, c);
        (await DualSubtree.DescendantsAsync(db, r, 100, TreeSide.Right))!.Select(f => f.MemberId).Should().BeEquivalentTo(b);
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Por_encima_del_tope_devuelve_nulo() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = SqlServerReportes.NuevoId();
        ReportesDb.NodoBinario(db, r, null, ReportesDb.Ruta(r), TreeSide.Left);
        for (var i = 0; i < 4; i++)
        {
            var h = SqlServerReportes.NuevoId();
            ReportesDb.NodoBinario(db, h, r, ReportesDb.Ruta(r, h), i % 2 == 0 ? TreeSide.Left : TreeSide.Right);
        }
        await db.SaveChangesAsync();

        (await DualSubtree.DescendantsAsync(db, r, 3)).Should().BeNull();
        (await DualSubtree.DescendantsAsync(db, r, 4)).Should().HaveCount(4);
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Una_pierna_de_mil_quinientos_niveles_no_choca_con_MAXRECURSION() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = SqlServerReportes.NuevoId();
        ReportesDb.NodoBinario(db, r, null, ReportesDb.Ruta(r), TreeSide.Left);
        var padre = r;
        for (var i = 0; i < 1500; i++)
        {
            var h = SqlServerReportes.NuevoId();
            // La ruta no importa a la CTE: se pone corta para no inflar la prueba.
            ReportesDb.NodoBinario(db, h, padre, ReportesDb.Ruta(r, h), TreeSide.Left);
            padre = h;
        }
        await db.SaveChangesAsync();

        var filas = await DualSubtree.DescendantsAsync(db, r, DualSubtree.MaxRowsForCte);

        filas.Should().HaveCount(1500);
        filas!.Max(f => f.Depth).Should().Be(1500);
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public async Task Cada_paso_de_la_CTE_es_un_seek_por_ParentMemberId()
    {
        var plan = await PlanDeEjecucion.EstimadoAsync("""
            DECLARE @r nvarchar(450) = N'NADIE';
            WITH sub AS (
                SELECT d.MemberId, 1 AS Depth, CAST(d.Side AS int) AS Leg FROM DualTeamTree d
                WHERE d.ParentMemberId = @r AND d.IsDeleted = 0
                UNION ALL
                SELECT c.MemberId, s.Depth + 1, s.Leg FROM sub s
                JOIN DualTeamTree c ON c.ParentMemberId = s.MemberId AND c.IsDeleted = 0)
            SELECT TOP (20001) MemberId, Depth, Leg FROM sub OPTION (MAXRECURSION 32767);
            """);

        plan.Should().Contain("IX_DualTeamTree_ParentMemberId").And.Contain(PlanDeEjecucion.Seek);
        plan.Should().NotContain("Key Lookup", "el índice incluye MemberId, Side e IsDeleted (M10)");
    }

    [SqlServerFact]
    public Task Mi_equipo_por_la_CTE_da_lo_mismo_que_por_la_ruta() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = SqlServerReportes.NuevoId(); var a = SqlServerReportes.NuevoId(); var b = SqlServerReportes.NuevoId();
        foreach (var id in new[] { r, a, b }) ReportesDb.Miembro(db, id);
        ReportesDb.NodoBinario(db, r, null, ReportesDb.Ruta(r), TreeSide.Left);
        ReportesDb.NodoBinario(db, a, r, ReportesDb.Ruta(r, a), TreeSide.Left);
        ReportesDb.NodoBinario(db, b, a, ReportesDb.Ruta(r, a, b), TreeSide.Right);
        await db.SaveChangesAsync();
        var servicio = new DualTeamService(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));

        var filas = (await servicio.GetMyTeamGridAsync(r, new GridDataRequest { PageSize = 50 })).Items.ToList();

        filas.Select(f => (f.MemberId, f.Level, f.Leg)).Should().BeEquivalentTo(new[]
        {
            (a, 1, "Left"), (b, 2, "Left")
        });
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Mi_equipo_de_un_subarbol_normal_cabe_en_tiempo() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        // El subárbol más grande que cabe en la CTE: desde la raíz más grande se baja por el hijo que aún no
        // cabe, y cuando los dos caben se queda con el mayor (revisión: la medición con un equipo de 3 no decía nada).
        var raices = await db.DualTeamTree.AsNoTracking().Where(d => d.ParentMemberId == null)
            .OrderBy(d => d.MemberId).Select(d => d.MemberId).ToListAsync();
        var actual = raices.First();
        foreach (var r in raices)
            if (await DualSubtree.DescendantsAsync(db, r, DualSubtree.MaxRowsForCte) is null) { actual = r; break; }
        IReadOnlyList<DualSubtree.Row>? sub = await DualSubtree.DescendantsAsync(db, actual, DualSubtree.MaxRowsForCte);
        for (var i = 0; i < 2000 && sub is null; i++)
        {
            var hijos = await db.DualTeamTree.AsNoTracking().Where(d => d.ParentMemberId == actual)
                .OrderBy(d => d.MemberId).Select(d => d.MemberId).ToListAsync();
            var medidos = new List<(string Id, IReadOnlyList<DualSubtree.Row>? Sub)>();
            foreach (var h in hijos)
                medidos.Add((h, await DualSubtree.DescendantsAsync(db, h, DualSubtree.MaxRowsForCte)));
            var grande = medidos.FirstOrDefault(m => m.Sub is null);
            if (grande.Id is not null) { actual = grande.Id; continue; }
            var mayor = medidos.OrderByDescending(m => m.Sub!.Count).First();
            actual = mayor.Id; sub = mayor.Sub;
        }
        sub.Should().NotBeNull();
        var servicio = new DualTeamService(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));

        var cronometro = Stopwatch.StartNew();
        var pagina = await servicio.GetMyTeamGridAsync(actual, new GridDataRequest { PageSize = 20 });
        cronometro.Stop();

        _salida.WriteLine($"Mi equipo Dual de {actual}: {sub!.Count} nodos, {pagina.TotalCount} filas en {cronometro.ElapsedMilliseconds} ms");
        pagina.TotalCount.Should().Be(sub.Count);
        cronometro.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
        await tx.RollbackAsync();
    });
}
