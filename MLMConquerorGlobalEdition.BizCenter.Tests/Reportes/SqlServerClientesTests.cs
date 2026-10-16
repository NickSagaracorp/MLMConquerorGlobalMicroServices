using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// El CASE del contacto, los dos alcances y el nivel por la orden (N2) se traducen a SQL; el
/// índice de patrocinador y fecha existe.
/// </summary>
public class SqlServerClientesTests
{
    [SqlServerFact]
    public async Task Los_dos_alcances_y_el_contacto_anulado_funcionan_en_SQL()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var yo = SqlServerReportes.NuevoId();
        var a  = SqlServerReportes.NuevoId();
        var c1 = SqlServerReportes.NuevoId("TC");
        var c2 = SqlServerReportes.NuevoId("TC");
        ReportesDb.Miembro(db, yo);
        ReportesDb.Miembro(db, a, yo);
        ReportesDb.Miembro(db, c1, yo, MemberType.ExternalMember, email: $"{c1}@prueba.test".ToLowerInvariant(), telefono: "111");
        ReportesDb.Miembro(db, c2, a,  MemberType.ExternalMember, email: $"{c2}@prueba.test".ToLowerInvariant(), telefono: "222");
        ReportesDb.Nodo(db, yo, null, ReportesDb.Ruta(yo), 1);
        ReportesDb.Nodo(db, a,  yo,   ReportesDb.Ruta(yo, a), 2);
        ReportesDb.Nodo(db, c1, yo,   ReportesDb.Ruta(yo, c1), 2);
        ReportesDb.Nodo(db, c2, a,    ReportesDb.Ruta(yo, a, c2), 3);
        var orden = ReportesDb.Orden(db, c1, ReportesDb.Base, 1, 2);
        ReportesDb.Suscripcion(db, c1, 1, MembershipStatus.Active, ReportesDb.Base).LastOrderId = orden.Id;
        await db.SaveChangesAsync();
        var servicio = ClientesDelEquipoTests.Servicio(db);

        var equipo     = (await servicio.GetCustomersGridAsync(yo, new GridDataRequest { PageSize = 50 }, CustomerScope.Team)).Items.ToList();
        var personales = (await servicio.GetCustomersGridAsync(yo, new GridDataRequest { PageSize = 50 }, CustomerScope.Personal)).Items.ToList();
        var busqueda   = await servicio.GetCustomersGridAsync(yo, new GridDataRequest { PageSize = 50, Search = c2.ToLowerInvariant() + "@" }, CustomerScope.Team);

        equipo.Should().HaveCount(2);
        equipo.Single(c => c.MemberId == c2).Email.Should().BeNull();
        equipo.Single(c => c.MemberId == c2).Level.Should().Be(2);
        equipo.Single(c => c.MemberId == c1).MembershipLevel.Should().Be(
            await db.MembershipLevels.Where(l => l.Id == 2).Select(l => l.Name).SingleAsync(),
            "el nivel sale del producto más alto de la orden");
        personales.Should().ContainSingle().Which.MemberId.Should().Be(c1);
        busqueda.TotalCount.Should().Be(0, "el correo del cliente ajeno está anulado en la proyección");

        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task El_indice_de_patrocinador_y_fecha_existe()
    {
        await using var db = SqlServerReportes.Db();

        var n = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.indexes
            WHERE name = 'IX_MemberProfiles_SponsorMemberId_EnrollDate'
              AND object_id = OBJECT_ID('MemberProfiles')
            """).SingleAsync();

        n.Should().Be(1, "la migración IndicePatrocinadorYFechaDeAlta tiene que estar aplicada en local");
    }
}
