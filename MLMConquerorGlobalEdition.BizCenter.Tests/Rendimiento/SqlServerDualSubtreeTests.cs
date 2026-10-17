using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>Plan M (M17–M20): la CTE del binario, con árboles propios dentro de una transacción.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerDualSubtreeTests
{
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
}
