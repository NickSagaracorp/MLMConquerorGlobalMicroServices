using System.Data;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Events;
using Xunit.Abstractions;

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
    private readonly ITestOutputHelper _salida;

    public SqlServerEventosTests(ITestOutputHelper salida) => _salida = salida;

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

    /// <summary>
    /// R16: mide la raíz real con un evento con inscripciones de unos pocos miembros de su equipo,
    /// para «solo inscritos» y los totales. Freno grueso de 60 s, como en Actividad; la cifra se imprime.
    /// En ReadUncommitted para medir la consulta y no los bloqueos de otros procesos.
    /// </summary>
    [SqlServerFact]
    public Task La_raiz_con_un_evento_cabe_en_tiempo() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        var raiz = await db.GenealogyTree.AsNoTracking()
            .Where(g => g.ParentMemberId == null).OrderBy(g => g.Level).Select(g => g.MemberId).FirstAsync();
        var algunos = await db.GenealogyTree.AsNoTracking()
            .Where(g => g.ParentMemberId == raiz).Select(g => g.MemberId).Take(5).ToListAsync();
        var evento = "TJ-EV-" + Guid.NewGuid().ToString("N")[..12];
        db.CorporateEvents.Add(new CorporateEvent { Id = evento, Title = "Medición", EventDate = new DateTime(2026, 12, 1), IsActive = true, CreatedBy = "prueba" });
        await db.SaveChangesAsync();
        foreach (var (m, i) in algunos.Append(raiz).Select((m, i) => (m, i)))
            db.CorporateEventRegistrations.Add(TeamEventRegistrationsServiceTests.Inscripcion(evento, m, 1, new DateTime(2026, 3, 1), $"{evento}-{i}"));
        await db.SaveChangesAsync();
        var servicio = TeamEventRegistrationsServiceTests.Servicio(db);

        var cronometro = Stopwatch.StartNew();
        var rejilla = await servicio.GetGridAsync(raiz, evento, registeredOnly: true, new GridDataRequest { PageSize = 20 });
        var totales = await servicio.GetTotalsAsync(raiz, evento);
        cronometro.Stop();

        _salida.WriteLine($"Eventos de {raiz}: {rejilla.Value!.TotalCount} inscritos, {totales.Value!.Tickets} entradas en {cronometro.ElapsedMilliseconds} ms");
        rejilla.Value.TotalCount.Should().Be(algunos.Count + 1);
        cronometro.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(60));

        await tx.RollbackAsync();
    });

    /// <summary>Plan Q: inscritos y totales unen con el rango del equipo, sin el TOP 1 correlacionado con IsDescendantOf.</summary>
    [SqlServerFact]
    public Task Inscritos_y_totales_unen_con_el_rango_sin_IsDescendantOf() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        var cap = new CapturadorDeSql();
        await using var db = cap.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var id = Nuevos();
        await TeamEventRegistrationsServiceTests.EscenarioAsync(db, id, sembrarRango: false);
        var servicio = TeamEventRegistrationsServiceTests.Servicio(db);
        cap.Limpiar();

        var inscritos = (await servicio.GetGridAsync(id.Yo, id.Evento, true, new GridDataRequest { PageSize = 50 })).Value!.Items.ToList();
        var totales   = (await servicio.GetTotalsAsync(id.Yo, id.Evento)).Value!;

        inscritos.Select(f => f.MemberId).Should().BeEquivalentTo(new[] { id.A, id.Yo });
        (totales.Registered, totales.Tickets).Should().Be((2, 6));
        cap.Textos.Should().NotContain(t => t.Contains("IsDescendantOf"));
        await tx.RollbackAsync();
    });

    /// <summary>
    /// Revision del bloque 5 (I4): «Todo el equipo» de la raiz grande (limitado) toma la pagina del arbol antes de unir los
    /// perfiles, como Mi equipo, y el total son los nodos vivos del equipo (el incluido), contados sin unir. Antes contaba la
    /// union entera en cada peticion (454 ms, unos 4 s con 1.000.000) y la pagina 1.000 ordenaba el rango unido (679 ms).
    /// </summary>
    [SqlServerFact]
    public Task Todo_el_equipo_grande_pagina_del_arbol_y_cuenta_los_nodos_sin_unir() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        const string raizMayor = "AMB-700829";
        var cap = new CapturadorDeSql();
        await using var db = cap.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        var evento = "TJ-EV-" + Guid.NewGuid().ToString("N")[..12];
        db.CorporateEvents.Add(new CorporateEvent { Id = evento, Title = "Medición", EventDate = new DateTime(2026, 12, 1), IsActive = true, CreatedBy = "prueba" });
        await db.SaveChangesAsync();
        var nodos = await db.GenealogyTree.AsNoTracking().CurrentUnder(await db.GenealogyTree.AsNoTracking().NodeOfAsync(raizMayor))
            .Select(g => g.MemberId).CountAsync();
        var servicio = TeamEventRegistrationsServiceTests.Servicio(db);
        await servicio.GetGridAsync(raizMayor, evento, false, new GridDataRequest { Page = 1, PageSize = 20 });   // calienta
        cap.Limpiar();

        var cronometro = Stopwatch.StartNew();
        var r = (await servicio.GetGridAsync(raizMayor, evento, false, new GridDataRequest { Page = 1000, PageSize = 20 })).Value!;
        cronometro.Stop();
        _salida.WriteLine($"Todo el equipo de {raizMayor}, pagina 1.000: {cronometro.ElapsedMilliseconds} ms, total {r.TotalCount}");

        r.Limited.Should().BeTrue();
        r.Items.Should().HaveCount(20);
        r.TotalCount.Should().Be(nodos);
        var pagina = cap.Textos.Single(t => t.Contains("FETCH NEXT") && t.Contains("[MemberProfiles]"));
        pagina.IndexOf("FETCH NEXT", StringComparison.Ordinal)
            .Should().BeLessThan(pagina.IndexOf("[MemberProfiles]", StringComparison.Ordinal), "la pagina sale del arbol");
        cap.Textos.Where(t => t.Contains("COUNT(")).Should().OnlyContain(t => !t.Contains("[MemberProfiles]"),
            "el total cuenta los nodos, no la union con los perfiles");
        await tx.RollbackAsync();
    });
}
