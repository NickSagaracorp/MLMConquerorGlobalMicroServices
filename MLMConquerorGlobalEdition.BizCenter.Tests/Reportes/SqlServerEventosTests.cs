using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Events;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Las subconsultas correlacionadas de entradas y fecha, la pertenencia por el nodo más reciente, el
/// orden por entradas y los totales se traducen a SQL; el índice único (EventId, ExternalRef) existe
/// y no distingue mayúsculas, que es lo que hará segura la importación futura (N5: no hay importador
/// en este plan, así que sus pruebas no están aquí).
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerEventosTests
{
    private static TeamEventRegistrationsServiceTests.Ids Nuevos() => new(
        SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId(),
        SqlServerReportes.NuevoId(), SqlServerReportes.NuevoId(),
        "TJ-EV-" + Guid.NewGuid().ToString("N")[..12], "TJ-EV-" + Guid.NewGuid().ToString("N")[..12]);

    [SqlServerFact]
    public Task La_rejilla_y_los_totales_se_traducen_a_SQL() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var id = Nuevos();
        await TeamEventRegistrationsServiceTests.EscenarioAsync(db, id, sembrarRango: false);
        var servicio = TeamEventRegistrationsServiceTests.Servicio(db);
        var porEntradas = new GridDataRequest { PageSize = 50 };
        porEntradas.Sorts.Add(new GridSort { Field = nameof(TeamEventRegistrationView.Tickets), Direction = "desc" });

        var inscritos = (await servicio.GetGridAsync(id.Yo, id.Evento, true, porEntradas)).Value!.Items.ToList();
        var todos     = (await servicio.GetGridAsync(id.Yo, id.Evento, false, new GridDataRequest { PageSize = 50 })).Value!.Items.ToList();
        var totales   = (await servicio.GetTotalsAsync(id.Yo, id.Evento)).Value!;

        inscritos.Select(f => f.MemberId).Should().Equal(id.A, id.Yo);
        inscritos[0].Tickets.Should().Be(4);
        inscritos[0].IsQualified.Should().BeTrue();
        todos.Should().HaveCount(3);
        (totales.Registered, totales.Tickets).Should().Be((2, 6));

        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task La_misma_referencia_no_entra_dos_veces_aunque_cambien_las_mayusculas() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var id = Nuevos();
        await TeamEventRegistrationsServiceTests.EscenarioAsync(db, id, sembrarRango: false);

        db.CorporateEventRegistrations.Add(
            TeamEventRegistrationsServiceTests.Inscripcion(id.Evento, id.B, 1, new DateTime(2026, 3, 1), $"{id.Evento}-r1".ToLowerInvariant()));
        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>("ya existe la referencia {0}-R1 para ese evento", id.Evento);
        await tx.RollbackAsync();
    });
}
