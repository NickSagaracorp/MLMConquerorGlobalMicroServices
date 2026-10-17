using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Cambios de estado: los miembros del alcance con un cambio registrado en el rango, con su estado
/// ACTUAL y la fecha del ÚLTIMO cambio. Inscripción y clientes por el nodo de genealogía más reciente
/// de cada miembro; Dual Team reduciendo primero por fecha y comprobando después la pertenencia al
/// árbol binario. «Calificado» es el de IQualificationService (N3) y el nivel sale de la orden (N2).
/// </summary>
public class TeamStatusChangesServiceTests
{
    internal sealed record Ids(string Yo, string E1, string E2, string E3, string E4, string R1, string Fuera, string Otro)
    {
        public static readonly Ids Fijos = new("AMB-000001", "AMB-000011", "MBR-000012", "AMB-000013",
                                               "AMB-000014", "AMB-000021", "AMB-000031", "AMB-000099");

        public IEnumerable<string> Todos => new[] { Yo, E1, E2, E3, E4, R1, Fuera, Otro };
    }

    internal static readonly DateTime Desde = new(2026, 3, 1);
    internal static readonly DateTime Hasta = new(2026, 3, 31);

    /// <summary>
    /// Genealogía: YO ─ E1 ─ E3; YO ─ E2 (cliente); YO ─ E4. OTRO ─ R1; OTRO ─ FUERA.
    /// Binario: YO ─izq─ E1 ─der─ E3; YO ─der─ R1. FUERA cuelga de OTRO.
    /// Cambios en marzo: E1 (cuenta y membresía), E2 (membresía a HoldByBilling), E3 (cuenta a
    /// Suspended), R1 (membresía) y FUERA (cuenta). E4 cambió en enero.
    /// La suscripción de E1 dice Lifestyle (1) pero su orden lleva Lifestyle y Elite: el nivel es Elite.
    /// </summary>
    internal static async Task EscenarioAsync(AppDbContext db, Ids id)
    {
        ReportesDb.Miembro(db, id.Yo);
        ReportesDb.Miembro(db, id.E1, id.Yo);
        ReportesDb.Miembro(db, id.E2, id.Yo, MemberType.ExternalMember);
        ReportesDb.Miembro(db, id.E3, id.E1, estado: MemberAccountStatus.Suspended);
        ReportesDb.Miembro(db, id.E4, id.Yo);
        ReportesDb.Miembro(db, id.Otro);
        ReportesDb.Miembro(db, id.R1, id.Otro);
        ReportesDb.Miembro(db, id.Fuera, id.Otro);

        ReportesDb.Nodo(db, id.Yo,    null,    ReportesDb.Ruta(id.Yo), 1);
        ReportesDb.Nodo(db, id.E1,    id.Yo,   ReportesDb.Ruta(id.Yo, id.E1), 2);
        ReportesDb.Nodo(db, id.E2,    id.Yo,   ReportesDb.Ruta(id.Yo, id.E2), 2);
        ReportesDb.Nodo(db, id.E3,    id.E1,   ReportesDb.Ruta(id.Yo, id.E1, id.E3), 3);
        ReportesDb.Nodo(db, id.E4,    id.Yo,   ReportesDb.Ruta(id.Yo, id.E4), 2);
        ReportesDb.Nodo(db, id.Otro,  null,    ReportesDb.Ruta(id.Otro), 1);
        ReportesDb.Nodo(db, id.R1,    id.Otro, ReportesDb.Ruta(id.Otro, id.R1), 2);
        ReportesDb.Nodo(db, id.Fuera, id.Otro, ReportesDb.Ruta(id.Otro, id.Fuera), 2);

        ReportesDb.NodoBinario(db, id.Yo,    null,    ReportesDb.Ruta(id.Yo), TreeSide.Left);
        ReportesDb.NodoBinario(db, id.E1,    id.Yo,   ReportesDb.Ruta(id.Yo, id.E1), TreeSide.Left);
        ReportesDb.NodoBinario(db, id.E3,    id.E1,   ReportesDb.Ruta(id.Yo, id.E1, id.E3), TreeSide.Right);
        ReportesDb.NodoBinario(db, id.R1,    id.Yo,   ReportesDb.Ruta(id.Yo, id.R1), TreeSide.Right);
        ReportesDb.NodoBinario(db, id.Otro,  null,    ReportesDb.Ruta(id.Otro), TreeSide.Left);
        ReportesDb.NodoBinario(db, id.Fuera, id.Otro, ReportesDb.Ruta(id.Otro, id.Fuera), TreeSide.Left);

        var ordenE1 = ReportesDb.Orden(db, id.E1, new DateTime(2026, 1, 10), 1, 3);
        var sE1 = ReportesDb.Suscripcion(db, id.E1, 1, MembershipStatus.Active, new DateTime(2026, 1, 10));
        sE1.LastOrderId = ordenE1.Id;
        var sE2 = ReportesDb.Suscripcion(db, id.E2, 2, MembershipStatus.HoldByBilling, new DateTime(2025, 11, 1));
        sE2.HoldDate = new DateTime(2026, 3, 10);
        var sE3 = ReportesDb.Suscripcion(db, id.E3, 1, MembershipStatus.OnHold, new DateTime(2025, 6, 1));
        sE3.HoldDate = new DateTime(2026, 2, 2);
        var sR1 = ReportesDb.Suscripcion(db, id.R1, 1, MembershipStatus.OnHold, new DateTime(2025, 6, 1));
        await db.SaveChangesAsync();

        // Lo que el interceptor apuntó al sembrar (altas de suscripción con la hora real) fuera:
        // el escenario escribe su propio historial.
        var todos = id.Todos.ToList();
        db.MembershipStatusHistories.RemoveRange(db.MembershipStatusHistories.Where(h => todos.Contains(h.MemberId)));
        db.MemberStatusHistories.RemoveRange(db.MemberStatusHistories.Where(h => todos.Contains(h.MemberId)));
        await db.SaveChangesAsync();

        db.MemberStatusHistories.AddRange(
            Cuenta(id.E1,    MemberAccountStatus.Pending, MemberAccountStatus.Active,    new DateTime(2026, 3, 5)),
            Cuenta(id.E3,    MemberAccountStatus.Active,  MemberAccountStatus.Suspended, new DateTime(2026, 3, 12)),
            Cuenta(id.Fuera, MemberAccountStatus.Active,  MemberAccountStatus.Inactive,  new DateTime(2026, 3, 15)),
            Cuenta(id.E4,    MemberAccountStatus.Active,  MemberAccountStatus.Inactive,  new DateTime(2026, 1, 1)));
        db.MembershipStatusHistories.AddRange(
            Membresia(id.E1, sE1.Id, 1, null, MembershipStatus.Pending, new DateTime(2026, 1, 10)),
            Membresia(id.E1, sE1.Id, 1, MembershipStatus.Pending, MembershipStatus.Active, new DateTime(2026, 3, 5)),
            Membresia(id.E2, sE2.Id, 2, MembershipStatus.Active, MembershipStatus.HoldByBilling, new DateTime(2026, 3, 10)),
            Membresia(id.R1, sR1.Id, 1, MembershipStatus.Active, MembershipStatus.OnHold, new DateTime(2026, 3, 15)));
        await db.SaveChangesAsync();
    }

    private static MemberStatusHistory Cuenta(string m, MemberAccountStatus de, MemberAccountStatus a, DateTime cuando) => new()
    {
        MemberId = m, OldStatus = de, NewStatus = a, ChangedAt = cuando, CreatedBy = "prueba"
    };

    private static MembershipStatusHistory Membresia(string m, string s, int nivel, MembershipStatus? de, MembershipStatus a, DateTime cuando) => new()
    {
        MemberId = m, SubscriptionId = s, MembershipLevelId = nivel, OldStatus = de, NewStatus = a, ChangedAt = cuando, CreatedBy = "prueba"
    };

    internal static TeamStatusChangesService Servicio(AppDbContext db) =>
        new(db, CalificadoEnLosEquiposTests.Calificacion(db));

    private static async Task<AppDbContext> BaseAsync()
    {
        var db = ReportesDb.Nueva();
        ReportesDb.Niveles(db);
        await EscenarioAsync(db, Ids.Fijos);
        return db;
    }

    private static GridDataRequest Pagina(string? buscar = null) => new() { Page = 1, PageSize = 50, Search = buscar };

    private static async Task<List<TeamStatusChangeView>> FilasAsync(AppDbContext db, StatusChangesScope alcance, GridDataRequest? pagina = null)
    {
        var r = await Servicio(db).GetGridAsync(Ids.Fijos.Yo, alcance, Desde, Hasta, pagina ?? Pagina());
        r.IsSuccess.Should().BeTrue();
        return r.Value!.Items.ToList();
    }

    [Fact]
    public async Task Inscripcion_lista_el_equipo_con_cambios_en_el_rango()
    {
        await using var db = await BaseAsync();
        var id = Ids.Fijos;

        (await FilasAsync(db, StatusChangesScope.Enrollment)).Select(f => f.MemberId)
            .Should().BeEquivalentTo(new[] { id.E1, id.E2, id.E3 }, "R1 y FUERA no son del equipo de inscripción y E4 cambió en enero");
    }

    [Fact]
    public async Task Clientes_solo_los_clientes_del_equipo()
    {
        await using var db = await BaseAsync();

        (await FilasAsync(db, StatusChangesScope.Customers)).Select(f => f.MemberId)
            .Should().Equal(Ids.Fijos.E2);
    }

    [Fact]
    public async Task Dual_team_va_por_el_arbol_binario_con_su_pierna()
    {
        await using var db = await BaseAsync();
        var id = Ids.Fijos;

        var filas = (await FilasAsync(db, StatusChangesScope.DualTeam)).ToDictionary(f => f.MemberId);

        filas.Keys.Should().BeEquivalentTo(new[] { id.E1, id.E3, id.R1 }, "E2 no está en el binario y FUERA cuelga de otro");
        filas[id.E1].Leg.Should().Be("Left");
        filas[id.E3].Leg.Should().Be("Left", "la pierna es la del hijo directo de YO en la ruta");
        filas[id.R1].Leg.Should().Be("Right");
        filas[id.E3].DualUplineMemberId.Should().Be(id.E1);
    }

    [Fact]
    public async Task Cada_fila_trae_el_estado_actual_y_la_fecha_del_ultimo_cambio()
    {
        await using var db = await BaseAsync();
        var id = Ids.Fijos;

        var filas = (await FilasAsync(db, StatusChangesScope.Enrollment)).ToDictionary(f => f.MemberId);

        var e1 = filas[id.E1];
        (e1.AccountStatus, e1.AccountStatusChangedAt, e1.MembershipStatus, e1.MembershipLevelName,
         e1.MembershipStatusChangedAt, e1.IsQualified, e1.SponsorMemberId)
            .Should().Be(("Active", (DateTime?)new DateTime(2026, 3, 5), "Active", "Travel Advantage - Elite",
                          (DateTime?)new DateTime(2026, 3, 5), true, id.Yo));
        e1.SponsorFullName.Should().Be($"Nombre {id.Yo}");

        var e2 = filas[id.E2];
        (e2.MembershipStatus, e2.MembershipStatusChangedAt, e2.IsQualified)
            .Should().Be(("HoldByBilling", (DateTime?)new DateTime(2026, 3, 10), false));
    }

    [Fact]
    public async Task Sin_historial_de_membresia_usa_la_fecha_propia_del_estado()
    {
        await using var db = await BaseAsync();

        var e3 = (await FilasAsync(db, StatusChangesScope.Enrollment)).Single(f => f.MemberId == Ids.Fijos.E3);

        (e3.AccountStatus, e3.AccountStatusChangedAt, e3.MembershipStatus, e3.MembershipStatusChangedAt, e3.IsQualified)
            .Should().Be(("Suspended", (DateTime?)new DateTime(2026, 3, 12), "OnHold", (DateTime?)new DateTime(2026, 2, 2), false));
    }

    [Fact]
    public async Task Calificado_es_el_de_la_regla_de_Nick_y_no_solo_membresia_activa()
    {
        // E1 está activo; con la marca quitada y un alta de hace dos años ya no califica.
        await using var db = await BaseAsync();
        var e1 = db.MemberProfiles.Single(m => m.MemberId == Ids.Fijos.E1);
        e1.EnrollDate = new DateTime(2024, 1, 1);
        await db.SaveChangesAsync();

        (await FilasAsync(db, StatusChangesScope.Enrollment)).Single(f => f.MemberId == Ids.Fijos.E1)
            .IsQualified.Should().BeFalse();
    }

    [Fact]
    public async Task Una_suscripcion_borrada_no_es_la_actual()
    {
        await using var db = await BaseAsync();
        ReportesDb.Suscripcion(db, Ids.Fijos.E1, 4, MembershipStatus.Cancelled, new DateTime(2026, 3, 20)).IsDeleted = true;
        await db.SaveChangesAsync();

        var e1 = (await FilasAsync(db, StatusChangesScope.Enrollment)).Single(f => f.MemberId == Ids.Fijos.E1);

        (e1.MembershipStatus, e1.MembershipLevelName).Should().Be(("Active", "Travel Advantage - Elite"));
    }

    [Fact]
    public async Task Un_miembro_con_un_nodo_obsoleto_en_el_equipo_no_sale()
    {
        // R8: FUERA tiene además un nodo antiguo bajo YO; manda el más reciente, que es de OTRO.
        await using var db = await BaseAsync();
        await ReportesDb.NodoObsoletoAsync(db, Ids.Fijos.Fuera, Ids.Fijos.Yo, ReportesDb.Ruta(Ids.Fijos.Yo, Ids.Fijos.Fuera), 2);

        (await FilasAsync(db, StatusChangesScope.Enrollment)).Select(f => f.MemberId)
            .Should().NotContain(Ids.Fijos.Fuera);
    }

    [Fact]
    public async Task La_ruta_binaria_no_sale_del_servidor_y_se_busca_por_nombre()
    {
        await using var db = await BaseAsync();

        var filas = await FilasAsync(db, StatusChangesScope.DualTeam, Pagina(Ids.Fijos.R1.ToLowerInvariant()));

        filas.Should().ContainSingle().Which.DualPath.Should().BeNull();
    }

    [Fact]
    public async Task Un_rango_de_mas_de_366_dias_se_rechaza()
    {
        await using var db = await BaseAsync();

        var r = await Servicio(db).GetGridAsync(
            Ids.Fijos.Yo, StatusChangesScope.Enrollment, new DateTime(2024, 1, 1), new DateTime(2025, 1, 1), Pagina());

        r.ErrorCode.Should().Be(ReportDateRange.TooLongCode);
    }

    [Fact]
    public async Task Un_rango_de_mas_de_tres_meses_atras_vale_aqui()
    {
        // N4 (3 meses) es solo de Actividad: Cambios de estado usa la regla común de 366 días.
        await using var db = await BaseAsync();

        var r = await Servicio(db).GetGridAsync(
            Ids.Fijos.Yo, StatusChangesScope.Enrollment, new DateTime(2025, 6, 1), Hasta, Pagina());

        r.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("enrollment", StatusChangesScope.Enrollment)]
    [InlineData("DUAL",       StatusChangesScope.DualTeam)]
    [InlineData("customers",  StatusChangesScope.Customers)]
    public void Las_tres_vistas_se_leen_de_la_ruta(string valor, StatusChangesScope esperado)
    {
        StatusChangesScopes.TryParse(valor, out var alcance).Should().BeTrue();
        alcance.Should().Be(esperado);
    }

    [Fact]
    public void Otra_vista_no_existe()
        => StatusChangesScopes.TryParse("binary", out _).Should().BeFalse();
}
