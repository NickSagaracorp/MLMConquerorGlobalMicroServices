using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Una fila por miembro del equipo que inscribió a alguien en el rango, con cuántos embajadores y
/// clientes, cuántos de cada nivel (el más alto de los productos de la ORDEN DE ALTA del inscrito,
/// corrección de Nick N2) y sus puntos de equipo de inscripción actuales.
/// </summary>
public class EnrollmentActivityServiceTests
{
    private const string Yo = "AMB-000001";
    private const string A  = "AMB-000002";
    private static readonly DateTime Desde = new(2026, 3, 1);
    private static readonly DateTime Hasta = new(2026, 3, 31);

    /// <summary>Hoy, para el servidor: marzo cabe en los 3 meses hacia atrás.</summary>
    internal static readonly RelojFijo Reloj = new(new DateTime(2026, 4, 15, 12, 0, 0));

    /// <summary>
    /// YO (500 pts) inscribe en marzo a N1 (embajador; alta Lifestyle y luego compra Elite) y N2
    /// (cliente, alta Lifestyle + VIP); en abril a N3. A (40 pts, de YO) inscribe a N4 (cliente, alta
    /// Lifestyle + Turbo) y N5 (embajador sin orden). OTRO, fuera del equipo, inscribe a N6.
    /// Las suscripciones dicen otra cosa a propósito: el nivel lo manda la orden.
    /// </summary>
    internal static async Task<AppDbContext> EquipoAsync(AppDbContext db, string yo, string a, string otro, string prefijo)
    {
        // YO, A y OTRO se dieron de alta antes del rango: no cuentan como inscritos de marzo.
        var antes = new DateTime(2025, 1, 10);
        ReportesDb.Miembro(db, yo, alta: antes);
        ReportesDb.Miembro(db, a, yo, alta: antes);
        ReportesDb.Miembro(db, otro, alta: antes);
        ReportesDb.Nodo(db, yo,   null, ReportesDb.Ruta(yo), 1);
        ReportesDb.Nodo(db, a,    yo,   ReportesDb.Ruta(yo, a), 2);
        ReportesDb.Nodo(db, otro, null, ReportesDb.Ruta(otro), 1);
        ReportesDb.Puntos(db, yo, 500);
        ReportesDb.Puntos(db, a, 40);

        ReportesDb.Miembro(db, $"{prefijo}1", yo, alta: new DateTime(2026, 3, 5));
        ReportesDb.Orden(db, $"{prefijo}1", new DateTime(2026, 3, 5), 1);
        ReportesDb.Orden(db, $"{prefijo}1", new DateTime(2026, 3, 25), 1, 3);
        ReportesDb.Suscripcion(db, $"{prefijo}1", 3, MembershipStatus.Active, new DateTime(2026, 3, 25));

        ReportesDb.Miembro(db, $"{prefijo}2", yo, MemberType.ExternalMember, alta: new DateTime(2026, 3, 31, 23, 0, 0));
        ReportesDb.Orden(db, $"{prefijo}2", new DateTime(2026, 3, 31, 23, 0, 0), 1, 2);
        ReportesDb.Suscripcion(db, $"{prefijo}2", 4, MembershipStatus.Active, new DateTime(2026, 3, 31));

        ReportesDb.Miembro(db, $"{prefijo}3", yo, alta: new DateTime(2026, 4, 1));

        ReportesDb.Miembro(db, $"{prefijo}4", a, MemberType.ExternalMember, alta: new DateTime(2026, 3, 20));
        ReportesDb.Orden(db, $"{prefijo}4", new DateTime(2026, 3, 20), 4, 1);

        ReportesDb.Miembro(db, $"{prefijo}5", a, alta: new DateTime(2026, 3, 21));
        ReportesDb.Suscripcion(db, $"{prefijo}5", 2, MembershipStatus.Active, new DateTime(2026, 3, 21));

        ReportesDb.Miembro(db, $"{prefijo}6", otro, alta: new DateTime(2026, 3, 15));
        await db.SaveChangesAsync();
        return db;
    }

    private static Task<AppDbContext> EquipoAsync()
    {
        var db = ReportesDb.Nueva();
        ReportesDb.Niveles(db);
        return EquipoAsync(db, Yo, A, "AMB-000099", "AMB-90000");
    }

    private static EnrollmentActivityService Servicio(AppDbContext db) => new(db, Reloj);

    private static GridDataRequest Pagina(string? buscar = null) => new() { Page = 1, PageSize = 50, Search = buscar };

    [Fact]
    public async Task Cuenta_por_patrocinador_tipo_y_nivel_de_la_orden_de_alta()
    {
        await using var db = await EquipoAsync();

        var r = await Servicio(db).GetGridAsync(Yo, Desde, Hasta, Pagina());

        r.IsSuccess.Should().BeTrue();
        var filas = r.Value!.Items.ToDictionary(f => f.MemberId);
        filas.Keys.Should().BeEquivalentTo(new[] { Yo, A });

        var yo = filas[Yo];
        (yo.Level, yo.Ambassadors, yo.Customers, yo.Level1, yo.Level2, yo.Level3, yo.Level4, yo.OtherLevel, yo.EnrollmentTeamPoints)
            .Should().Be((0, 1, 1, 1, 1, 0, 0, 0, 500));

        var a = filas[A];
        (a.Level, a.Ambassadors, a.Customers, a.Level1, a.Level2, a.Level3, a.Level4, a.OtherLevel, a.EnrollmentTeamPoints)
            .Should().Be((1, 1, 1, 0, 0, 0, 1, 1, 40));
    }

    [Fact]
    public async Task Fuera_del_rango_o_del_equipo_no_cuenta()
    {
        await using var db = await EquipoAsync();

        var r = await Servicio(db).GetGridAsync(Yo, Desde, Hasta, Pagina());

        r.Value!.Items.Sum(f => f.Ambassadors + f.Customers).Should().Be(4, "N3 es de abril y N6 de otro equipo");
    }

    [Fact]
    public async Task Busca_por_nombre_y_ordena_por_cualquier_columna()
    {
        await using var db = await EquipoAsync();
        var pagina = Pagina(A.ToLowerInvariant());
        var porNivel4 = Pagina();
        porNivel4.Sorts.Add(new GridSort { Field = nameof(EnrollmentActivityRowView.Level4), Direction = "desc" });

        (await Servicio(db).GetGridAsync(Yo, Desde, Hasta, pagina))
            .Value!.Items.Should().ContainSingle().Which.MemberId.Should().Be(A);
        (await Servicio(db).GetGridAsync(Yo, Desde, Hasta, porNivel4))
            .Value!.Items.First().MemberId.Should().Be(A);
    }

    [Fact]
    public async Task Un_inicio_de_hace_mas_de_tres_meses_se_rechaza()
    {
        await using var db = await EquipoAsync();

        var r = await Servicio(db).GetGridAsync(Yo, new DateTime(2026, 1, 14), Hasta, Pagina());

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(ReportDateRange.TooOldCode);
    }

    [Fact]
    public async Task Un_fin_posterior_a_hoy_se_rechaza()
    {
        await using var db = await EquipoAsync();

        var r = await Servicio(db).GetGridAsync(Yo, Desde, new DateTime(2026, 4, 16), Pagina());

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(ReportDateRange.InFutureCode);
    }

    [Fact]
    public async Task Sin_nodo_de_genealogia_no_hay_filas()
    {
        await using var db = await EquipoAsync();

        var r = await Servicio(db).GetGridAsync("AMB-555555", Desde, Hasta, Pagina());

        r.IsSuccess.Should().BeTrue();
        r.Value!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Las_columnas_son_los_niveles_1_a_4_con_su_nombre()
    {
        await using var db = await EquipoAsync();

        var columnas = await Servicio(db).GetColumnsAsync();

        columnas.Select(c => (c.LevelId, c.Name)).Should().Equal(
            (1, "Lifestyle Ambassador"), (2, "Travel Advantage - VIP"),
            (3, "Travel Advantage - Elite"), (4, "Travel Advantage - Turbo"));
    }
}
