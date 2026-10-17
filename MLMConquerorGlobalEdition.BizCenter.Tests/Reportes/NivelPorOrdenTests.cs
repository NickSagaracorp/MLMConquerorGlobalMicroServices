using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Corrección de Nick N2: el nivel de membresía lo da el producto de la orden (el más alto de sus
/// productos), no la suscripción. Estas son las dos variantes por lote que usan los reportes.
/// </summary>
public class NivelPorOrdenTests
{
    [Fact]
    public async Task Por_lote_de_ordenes_da_el_nivel_mas_alto_de_cada_una()
    {
        await using var db = ReportesDb.Nueva();
        var conDos   = ReportesDb.Orden(db, "AMB-000001", ReportesDb.Base, 1, 3);
        var conUno   = ReportesDb.Orden(db, "AMB-000002", ReportesDb.Base, 1);
        var sinNivel = ReportesDb.Orden(db, "AMB-000003", ReportesDb.Base, (int?)null);
        var fuera    = ReportesDb.Orden(db, "AMB-000004", ReportesDb.Base, 4);
        await db.SaveChangesAsync();
        var pedidas = new[] { conDos.Id, conUno.Id, sinNivel.Id };

        var niveles = await db.GetHighestMembershipLevelIdsAsync(
            db.Orders.Where(o => pedidas.Contains(o.Id)).Select(o => o.Id));

        niveles.Should().BeEquivalentTo(new Dictionary<string, int> { [conDos.Id] = 3, [conUno.Id] = 1 });
        niveles.Should().NotContainKey(fuera.Id);
    }

    [Fact]
    public async Task El_nivel_de_una_suscripcion_es_el_de_su_ultima_orden_y_si_no_tiene_el_suyo()
    {
        await using var db = ReportesDb.Nueva();
        var orden = ReportesDb.Orden(db, "AMB-000001", ReportesDb.Base, 1, 2);
        var conOrden = ReportesDb.Suscripcion(db, "AMB-000001", 1, MembershipStatus.Active, ReportesDb.Base);
        conOrden.LastOrderId = orden.Id;
        var sinOrden = ReportesDb.Suscripcion(db, "AMB-000002", 3, MembershipStatus.Active, ReportesDb.Base);
        var ordenSinNivel = ReportesDb.Orden(db, "AMB-000003", ReportesDb.Base, (int?)null);
        var conOrdenSinNivel = ReportesDb.Suscripcion(db, "AMB-000003", 4, MembershipStatus.Active, ReportesDb.Base);
        conOrdenSinNivel.LastOrderId = ordenSinNivel.Id;
        await db.SaveChangesAsync();

        var niveles = await db.GetMembershipLevelIdsBySubscriptionAsync(new[] { conOrden, sinOrden, conOrdenSinNivel });

        niveles[conOrden.Id].Should().Be(2, "manda el producto de la orden, no la suscripción");
        niveles[sinOrden.Id].Should().Be(3, "sin orden, la reserva es el nivel de la suscripción");
        niveles[conOrdenSinNivel.Id].Should().Be(4, "una orden sin productos de membresía tampoco da nivel");
    }
}
