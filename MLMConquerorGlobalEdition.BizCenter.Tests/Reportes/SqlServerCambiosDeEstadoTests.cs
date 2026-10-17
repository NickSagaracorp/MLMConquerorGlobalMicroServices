using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// El UNION de los dos historiales, la pertenencia por el nodo más reciente y el prefijo binario se
/// traducen a SQL y dan lo mismo que en memoria; el interceptor escribe de verdad en las tablas
/// nuevas; «Calificado» (N3) se calcula igual contra la base.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerCambiosDeEstadoTests
{
    private static TeamStatusChangesServiceTests.Ids Nuevos() => new(
        SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId("TC"),
        SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId(),
        SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId());

    [SqlServerFact]
    public Task Las_tres_vistas_se_traducen_a_SQL() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var id = Nuevos();
        await TeamStatusChangesServiceTests.EscenarioAsync(db, id);
        var servicio = TeamStatusChangesServiceTests.Servicio(db);
        async Task<List<TeamStatusChangeView>> Filas(StatusChangesScope alcance) =>
            (await servicio.GetGridAsync(id.Yo, alcance, TeamStatusChangesServiceTests.Desde, TeamStatusChangesServiceTests.Hasta,
                new GridDataRequest { PageSize = 50 })).Value!.Items.ToList();

        var inscripcion = await Filas(StatusChangesScope.Enrollment);
        inscripcion.Select(f => f.MemberId).Should().BeEquivalentTo(new[] { id.E1, id.E2, id.E3 });
        (await Filas(StatusChangesScope.Customers)).Select(f => f.MemberId).Should().Equal(id.E2);
        (await Filas(StatusChangesScope.DualTeam)).Select(f => f.MemberId).Should().BeEquivalentTo(new[] { id.E1, id.E3, id.R1 });

        var e1 = inscripcion.Single(f => f.MemberId == id.E1);
        (e1.MembershipLevelName, e1.MembershipStatusChangedAt, e1.IsQualified).Should().Be(
            (await db.MembershipLevels.Where(l => l.Id == 3).Select(l => l.Name).SingleAsync(),
             (DateTime?)new DateTime(2026, 3, 5), true));

        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task El_interceptor_escribe_en_las_tablas_de_historial() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var yo = SqlServerReportes.NuevoId();
        var perfil = ReportesDb.Miembro(db, yo, estado: MemberAccountStatus.Pending);
        ReportesDb.Suscripcion(db, yo, 3, MembershipStatus.Pending, new DateTime(2026, 3, 1));
        await db.SaveChangesAsync();

        perfil.Status = MemberAccountStatus.Active;
        await db.SaveChangesAsync();

        (await db.MemberStatusHistories.CountAsync(h => h.MemberId == yo)).Should().Be(1);
        (await db.MembershipStatusHistories.CountAsync(h => h.MemberId == yo)).Should().Be(1);

        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Calificado_se_calcula_igual_en_SQL() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var reciente = SqlServerReportes.NuevoId();
        var antiguo  = SqlServerReportes.NuevoId();
        var marcado  = SqlServerReportes.NuevoId();
        ReportesDb.Miembro(db, reciente, alta: new DateTime(2026, 9, 1));
        ReportesDb.Miembro(db, antiguo,  alta: new DateTime(2024, 1, 1));
        ReportesDb.Miembro(db, marcado,  alta: new DateTime(2024, 1, 1)).KeepQualified = true;
        foreach (var m in new[] { reciente, antiguo, marcado })
            ReportesDb.Suscripcion(db, m, 2, MembershipStatus.Active, new DateTime(2024, 1, 1));
        await db.SaveChangesAsync();

        var r = await CalificadoEnLosEquiposTests.Calificacion(db).AreQualifiedAsync(new[] { reciente, antiguo, marcado });

        r.Should().BeEquivalentTo(new Dictionary<string, bool> { [reciente] = true, [antiguo] = false, [marcado] = true });

        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Los_indices_de_los_historiales_existen() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();

        var n = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.indexes
            WHERE name IN ('IX_MemberStatusHistories_MemberId_ChangedAt', 'IX_MemberStatusHistories_ChangedAt',
                           'IX_MembershipStatusHistories_MemberId_ChangedAt', 'IX_MembershipStatusHistories_ChangedAt')
            """).SingleAsync();

        n.Should().Be(4, "la migración HistorialDeEstados tiene que estar aplicada en local");
    });
}
