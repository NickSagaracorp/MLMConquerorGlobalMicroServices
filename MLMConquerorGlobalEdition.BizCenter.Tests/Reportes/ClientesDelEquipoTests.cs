using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Clientes del equipo: alcance Personales o Equipo, nivel relativo y datos de contacto solo de los
/// clientes personales. El contacto se anula en la proyección, así que tampoco se puede buscar.
/// </summary>
public class ClientesDelEquipoTests
{
    private const string Yo = "AMB-000001";

    internal static EnrollmentTeamService Servicio(AppDbContext db) => new(
        db,
        new RankComputationService(db, new RankQualificationService(
            db, new EnrollmentTeamPointsService(db), new PersonalCustomerPointsService(db))),
        new EnrollmentTeamPointsService(db),
        CalificadoEnLosEquiposTests.Calificacion(db));

    /// <summary>
    /// YO ─┬─ C1 (cliente personal de YO)
    ///     └─ A (embajador) ── C2 (cliente de A)
    /// OTRO ── FUERA (cliente de otro equipo)
    /// </summary>
    private static async Task<AppDbContext> EquipoAsync()
    {
        var db = ReportesDb.Nueva();
        ReportesDb.Miembro(db, Yo);
        ReportesDb.Miembro(db, "AMB-000002", Yo);
        ReportesDb.Miembro(db, "MBR-000001", Yo, MemberType.ExternalMember, email: "c1@prueba.test", telefono: "111");
        ReportesDb.Miembro(db, "MBR-000002", "AMB-000002", MemberType.ExternalMember, email: "c2@prueba.test", telefono: "222");
        ReportesDb.Miembro(db, "MBR-000009", "AMB-000099", MemberType.ExternalMember);
        ReportesDb.Nodo(db, Yo,           null,         ReportesDb.Ruta(Yo), 1);
        ReportesDb.Nodo(db, "AMB-000002", Yo,           ReportesDb.Ruta(Yo, "AMB-000002"), 2);
        ReportesDb.Nodo(db, "MBR-000001", Yo,           ReportesDb.Ruta(Yo, "MBR-000001"), 2);
        ReportesDb.Nodo(db, "MBR-000002", "AMB-000002", ReportesDb.Ruta(Yo, "AMB-000002", "MBR-000002"), 3);
        ReportesDb.Nodo(db, "MBR-000009", "AMB-000099", ReportesDb.Ruta("AMB-000099", "MBR-000009"), 2);
        await db.SaveChangesAsync();
        return db;
    }

    private static GridDataRequest Pagina(string? buscar = null) => new() { Page = 1, PageSize = 50, Search = buscar };

    [Fact]
    public async Task Equipo_lista_los_clientes_del_subarbol_con_su_nivel_relativo()
    {
        await using var db = await EquipoAsync();

        var r = await Servicio(db).GetCustomersGridAsync(Yo, Pagina(), CustomerScope.Team);

        r.Items.Select(c => (c.MemberId, c.Level)).Should().BeEquivalentTo(new[]
        {
            ("MBR-000001", 1),
            ("MBR-000002", 2)
        });
    }

    [Fact]
    public async Task Personales_solo_los_que_inscribio_el_que_mira()
    {
        await using var db = await EquipoAsync();

        var r = await Servicio(db).GetCustomersGridAsync(Yo, Pagina(), CustomerScope.Personal);

        r.Items.Should().ContainSingle().Which.MemberId.Should().Be("MBR-000001");
        r.Items.Single().Level.Should().Be(1);
        r.Items.Single().Email.Should().Be("c1@prueba.test");
    }

    [Fact]
    public async Task En_equipo_el_contacto_solo_se_ve_de_los_personales()
    {
        await using var db = await EquipoAsync();

        var r = (await Servicio(db).GetCustomersGridAsync(Yo, Pagina(), CustomerScope.Team)).Items.ToList();

        var c1 = r.Single(c => c.MemberId == "MBR-000001");
        var c2 = r.Single(c => c.MemberId == "MBR-000002");
        (c1.IsPersonal, c1.Email, c1.Phone).Should().Be((true, "c1@prueba.test", "111"));
        (c2.IsPersonal, c2.Email, c2.Phone).Should().Be((false, (string?)null, (string?)null));
    }

    [Fact]
    public async Task Buscar_por_el_correo_de_un_cliente_ajeno_no_lo_delata()
    {
        await using var db = await EquipoAsync();

        var r = await Servicio(db).GetCustomersGridAsync(Yo, Pagina("c2@prueba"), CustomerScope.Team);

        r.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task El_GET_antiguo_tambien_oculta_el_contacto_ajeno()
    {
        await using var db = await EquipoAsync();

        var r = await Servicio(db).GetCustomersAsync(Yo, 1, 50, null);

        r.Items.Single(c => c.MemberId == "MBR-000002").Email.Should().BeNull();
        r.Items.Single(c => c.MemberId == "MBR-000001").Email.Should().Be("c1@prueba.test");
    }

    [Fact]
    public async Task El_nivel_de_membresia_sale_del_producto_de_la_orden()
    {
        // Corrección de Nick N2: la suscripción dice Lifestyle Ambassador (1), pero su orden lleva
        // Lifestyle Ambassador y VIP: el nivel es VIP, el más alto de la orden.
        await using var db = await EquipoAsync();
        ReportesDb.Niveles(db);
        var orden = ReportesDb.Orden(db, "MBR-000001", ReportesDb.Base, 1, 2);
        ReportesDb.Suscripcion(db, "MBR-000001", 1, MembershipStatus.Active, ReportesDb.Base).LastOrderId = orden.Id;
        ReportesDb.Suscripcion(db, "MBR-000002", 3, MembershipStatus.Active, ReportesDb.Base);
        await db.SaveChangesAsync();

        var r = (await Servicio(db).GetCustomersGridAsync(Yo, Pagina(), CustomerScope.Team)).Items.ToList();

        r.Single(c => c.MemberId == "MBR-000001").MembershipLevel.Should().Be("Travel Advantage - VIP");
        r.Single(c => c.MemberId == "MBR-000002").MembershipLevel.Should().Be("Travel Advantage - Elite",
            "sin orden, la reserva es el nivel de la suscripción");
    }

    [Fact]
    public async Task Un_cliente_con_dos_nodos_sale_una_vez_y_por_su_nodo_mas_reciente()
    {
        // R8: C3 tiene un nodo obsoleto bajo YO y el actual bajo OTRO: ya no es del equipo.
        // C4 tiene dos nodos bajo YO: sale una vez, con el nivel del más reciente.
        await using var db = await EquipoAsync();
        ReportesDb.Miembro(db, "MBR-000003", "AMB-000099", MemberType.ExternalMember);
        ReportesDb.Miembro(db, "MBR-000004", "AMB-000002", MemberType.ExternalMember);
        await ReportesDb.NodoObsoletoAsync(db, "MBR-000003", Yo, ReportesDb.Ruta(Yo, "MBR-000003"), 2);
        await ReportesDb.NodoObsoletoAsync(db, "MBR-000004", Yo, ReportesDb.Ruta(Yo, "MBR-000004"), 2);
        ReportesDb.Nodo(db, "MBR-000003", "AMB-000099", ReportesDb.Ruta("AMB-000099", "MBR-000003"), 2);
        ReportesDb.Nodo(db, "MBR-000004", "AMB-000002", ReportesDb.Ruta(Yo, "AMB-000002", "MBR-000004"), 3);
        await db.SaveChangesAsync();

        var r = (await Servicio(db).GetCustomersGridAsync(Yo, Pagina(), CustomerScope.Team)).Items.ToList();

        r.Should().NotContain(c => c.MemberId == "MBR-000003");
        r.Where(c => c.MemberId == "MBR-000004").Should().ContainSingle().Which.Level.Should().Be(2);
    }

    [Fact]
    public async Task El_prefijo_del_que_mira_no_casa_con_un_id_que_empieza_igual()
    {
        // Si la ruta del que mira no acabara en barra, "/AMB-000001" casaría con "/AMB-0000019/...".
        await using var db = ReportesDb.Nueva();
        ReportesDb.Miembro(db, Yo);
        ReportesDb.Miembro(db, "AMB-0000019");
        ReportesDb.Miembro(db, "MBR-000077", "AMB-0000019", MemberType.ExternalMember);
        ReportesDb.Nodo(db, Yo,            null,          "/" + Yo, 1);
        ReportesDb.Nodo(db, "AMB-0000019", null,          ReportesDb.Ruta("AMB-0000019"), 1);
        ReportesDb.Nodo(db, "MBR-000077",  "AMB-0000019", ReportesDb.Ruta("AMB-0000019", "MBR-000077"), 2);
        await db.SaveChangesAsync();

        var r = await Servicio(db).GetCustomersGridAsync(Yo, Pagina(), CustomerScope.Team);

        r.TotalCount.Should().Be(0);
    }

    [Theory]
    [InlineData("personal", CustomerScope.Personal)]
    [InlineData("PERSONAL", CustomerScope.Personal)]
    [InlineData("team",     CustomerScope.Team)]
    [InlineData(null,       CustomerScope.Team)]
    [InlineData("otra",     CustomerScope.Team)]
    public void El_alcance_de_la_query_por_defecto_es_equipo(string? valor, CustomerScope esperado)
        => CustomerScopes.Parse(valor).Should().Be(esperado);
}
