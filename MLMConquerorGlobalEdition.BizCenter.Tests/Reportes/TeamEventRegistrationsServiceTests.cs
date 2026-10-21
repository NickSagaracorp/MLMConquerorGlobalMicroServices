using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Events;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Inscripciones a un evento dentro del equipo de inscripción del que mira (él incluido), con las
/// entradas sumadas, la primera fecha, si está calificado (IQualificationService) y su rango; y los
/// totales del equipo. Sin importación todavía (N5): con un evento sin inscripciones, sale vacío.
/// </summary>
public class TeamEventRegistrationsServiceTests
{
    internal sealed record Ids(string Yo, string A, string B, string Fuera, string Otro, string Evento, string Inactivo)
    {
        public static readonly Ids Fijos = new("AMB-000001", "AMB-000002", "AMB-000003", "AMB-000031", "AMB-000099", "EV-1", "EV-OFF");
    }

    /// <summary>
    /// YO ─ A, YO ─ B; OTRO ─ FUERA. En EV-1: YO 2 entradas (1 mar), A dos compras (1 entrada el
    /// 3 mar y 3 el 2 mar), FUERA 5 entradas. EV-OFF está desactivado.
    /// </summary>
    internal static async Task EscenarioAsync(AppDbContext db, Ids id, bool sembrarRango = true)
    {
        ReportesDb.Miembro(db, id.Yo);
        ReportesDb.Miembro(db, id.A, id.Yo);
        ReportesDb.Miembro(db, id.B, id.Yo);
        ReportesDb.Miembro(db, id.Otro);
        ReportesDb.Miembro(db, id.Fuera, id.Otro);
        ReportesDb.Nodo(db, id.Yo,    null,    ReportesDb.Ruta(id.Yo), 1);
        ReportesDb.Nodo(db, id.A,     id.Yo,   ReportesDb.Ruta(id.Yo, id.A), 2);
        ReportesDb.Nodo(db, id.B,     id.Yo,   ReportesDb.Ruta(id.Yo, id.B), 2);
        ReportesDb.Nodo(db, id.Otro,  null,    ReportesDb.Ruta(id.Otro), 1);
        ReportesDb.Nodo(db, id.Fuera, id.Otro, ReportesDb.Ruta(id.Otro, id.Fuera), 2);
        ReportesDb.Suscripcion(db, id.A, 3, MembershipStatus.Active, new DateTime(2026, 1, 1));

        db.CorporateEvents.AddRange(
            new CorporateEvent { Id = id.Evento,   Title = "Convención", EventDate = new DateTime(2026, 11, 20), IsActive = true,  CreatedBy = "prueba" },
            new CorporateEvent { Id = id.Inactivo, Title = "Pasado",     EventDate = new DateTime(2025, 11, 20), IsActive = false, CreatedBy = "prueba" });

        if (sembrarRango)
        {
            db.RankDefinitions.Add(new RankDefinition { Id = 1, Name = "Bronze", SortOrder = 1, CreatedBy = "prueba" });
            db.MemberRankHistories.Add(new MemberRankHistory { MemberId = id.A, RankDefinitionId = 1, AchievedAt = new DateTime(2026, 2, 1), CreatedBy = "prueba" });
        }
        await db.SaveChangesAsync();

        db.CorporateEventRegistrations.AddRange(
            Inscripcion(id.Evento, id.Yo,    2, new DateTime(2026, 3, 1), $"{id.Evento}-R1"),
            Inscripcion(id.Evento, id.A,     1, new DateTime(2026, 3, 3), $"{id.Evento}-R2"),
            Inscripcion(id.Evento, id.A,     3, new DateTime(2026, 3, 2), $"{id.Evento}-R3"),
            Inscripcion(id.Evento, id.Fuera, 5, new DateTime(2026, 3, 1), $"{id.Evento}-R4"));
        await db.SaveChangesAsync();
    }

    internal static CorporateEventRegistration Inscripcion(string evento, string miembro, int entradas, DateTime cuando, string referencia) => new()
    {
        EventId = evento, MemberId = miembro, Tickets = entradas, RegisteredAt = cuando, ExternalRef = referencia, CreatedBy = "prueba"
    };

    internal static TeamEventRegistrationsService Servicio(AppDbContext db) =>
        new(db, CalificadoEnLosEquiposTests.Calificacion(db));

    private static async Task<AppDbContext> BaseAsync()
    {
        var db = ReportesDb.Nueva();
        ReportesDb.Niveles(db);
        await EscenarioAsync(db, Ids.Fijos);
        return db;
    }

    private static GridDataRequest Pagina() => new() { Page = 1, PageSize = 50 };

    [Fact]
    public async Task Solo_inscritos_del_equipo_con_las_entradas_sumadas_y_la_primera_fecha()
    {
        await using var db = await BaseAsync();
        var id = Ids.Fijos;

        var r = await Servicio(db).GetGridAsync(id.Yo, id.Evento, registeredOnly: true, Pagina());

        var filas = r.Value!.Items.ToDictionary(f => f.MemberId);
        filas.Keys.Should().BeEquivalentTo(new[] { id.Yo, id.A }, "B no compró y FUERA no es del equipo");
        (filas[id.A].Tickets, filas[id.A].RegisteredAt, filas[id.A].Registered).Should().Be((4, (DateTime?)new DateTime(2026, 3, 2), true));
        filas[id.Yo].Tickets.Should().Be(2);
    }

    [Fact]
    public async Task Todo_el_equipo_con_No_para_quien_no_se_inscribio()
    {
        await using var db = await BaseAsync();
        var id = Ids.Fijos;

        var r = await Servicio(db).GetGridAsync(id.Yo, id.Evento, registeredOnly: false, Pagina());

        var b = r.Value!.Items.Single(f => f.MemberId == id.B);
        (b.Registered, b.Tickets, b.RegisteredAt).Should().Be((false, 0, (DateTime?)null));
        r.Value.Items.Select(f => f.MemberId).Should().NotContain(id.Fuera);
    }

    [Fact]
    public async Task Los_totales_son_solo_del_equipo()
    {
        await using var db = await BaseAsync();
        var id = Ids.Fijos;

        var t = await Servicio(db).GetTotalsAsync(id.Yo, id.Evento);

        (t.Value!.Registered, t.Value.Tickets).Should().Be((2, 6));
    }

    [Fact]
    public async Task Cada_fila_dice_si_esta_calificado_y_su_rango()
    {
        await using var db = await BaseAsync();
        var id = Ids.Fijos;

        var a = (await Servicio(db).GetGridAsync(id.Yo, id.Evento, true, Pagina()))
            .Value!.Items.Single(f => f.MemberId == id.A);

        (a.IsQualified, a.RankName).Should().Be((true, "Bronze"));
    }

    [Fact]
    public async Task Un_evento_desactivado_o_inexistente_es_EVENT_NOT_FOUND()
    {
        await using var db = await BaseAsync();
        var id = Ids.Fijos;
        var servicio = Servicio(db);

        (await servicio.GetGridAsync(id.Yo, id.Inactivo, true, Pagina())).ErrorCode.Should().Be(TeamEventErrors.EventNotFound);
        (await servicio.GetTotalsAsync(id.Yo, "EV-NO")).ErrorCode.Should().Be(TeamEventErrors.EventNotFound);
    }

    [Fact]
    public async Task Solo_se_listan_los_eventos_activos()
    {
        await using var db = await BaseAsync();

        (await Servicio(db).GetActiveEventsAsync()).Select(e => e.Id).Should().Equal(Ids.Fijos.Evento);
    }

    [Fact]
    public async Task Se_ordena_por_entradas()
    {
        await using var db = await BaseAsync();
        var id = Ids.Fijos;
        var pagina = Pagina();
        pagina.Sorts.Add(new GridSort { Field = nameof(TeamEventRegistrationView.Tickets), Direction = "desc" });

        (await Servicio(db).GetGridAsync(id.Yo, id.Evento, true, pagina))
            .Value!.Items.First().MemberId.Should().Be(id.A);
    }

    [Fact]
    public async Task Sin_inscripciones_importadas_sale_vacio()
    {
        // N5: todavía no hay importación; un evento activo sin inscripciones da una rejilla vacía.
        await using var db = await BaseAsync();
        db.CorporateEvents.Add(new CorporateEvent { Id = "EV-2", Title = "Nuevo", EventDate = new DateTime(2026, 12, 1), IsActive = true, CreatedBy = "prueba" });
        await db.SaveChangesAsync();

        var r = await Servicio(db).GetGridAsync(Ids.Fijos.Yo, "EV-2", true, Pagina());
        var t = await Servicio(db).GetTotalsAsync(Ids.Fijos.Yo, "EV-2");

        r.Value!.Items.Should().BeEmpty();
        (t.Value!.Registered, t.Value.Tickets).Should().Be((0, 0));
    }

    [Fact]
    public async Task Un_miembro_que_se_fue_del_equipo_no_cuenta_aunque_le_quede_un_nodo_antiguo()
    {
        // R8: FUERA tuvo un nodo bajo YO; manda el más reciente, que es de OTRO.
        await using var db = await BaseAsync();
        await ReportesDb.NodoObsoletoAsync(db, Ids.Fijos.Fuera, Ids.Fijos.Yo, ReportesDb.Ruta(Ids.Fijos.Yo, Ids.Fijos.Fuera), 2);

        var t = await Servicio(db).GetTotalsAsync(Ids.Fijos.Yo, Ids.Fijos.Evento);

        (t.Value!.Registered, t.Value.Tickets).Should().Be((2, 6));
    }

    /// <summary>Plan Q (DQ7): «Todo el equipo» por encima de TeamGridLimits.FreeMaxRows ordena por estructura y lo avisa.</summary>
    [Theory]
    [InlineData(30_000, true)]
    [InlineData(2, false)]
    public async Task Todo_el_equipo_por_encima_del_limite_ordena_por_estructura_y_lo_avisa(int tamano, bool limitado)
    {
        await using var db = ReportesDb.Nueva();
        var id = Ids.Fijos;
        await EscenarioAsync(db, id);
        ReportesDb.Puntos(db, id.Yo, 0).EnrollmentTeamSize = tamano;
        await db.SaveChangesAsync();

        var r = (await Servicio(db).GetGridAsync(id.Yo, id.Evento, false, new GridDataRequest { PageSize = 50 })).Value!;

        r.Limited.Should().Be(limitado);
        r.Items.Should().HaveCount(3);
        r.TotalCount.Should().Be(3, "el total es el de las filas listadas, no el contador guardado (revision del bloque 5, I1)");
    }
}
