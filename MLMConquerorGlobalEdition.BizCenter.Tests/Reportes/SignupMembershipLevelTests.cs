using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>Plan Q (DQ10): SignupMembershipLevel aplica la regla N2 del Plan J.</summary>
public class SignupMembershipLevelTests
{
    [Fact]
    public async Task El_nivel_de_alta_es_el_mas_alto_del_primer_pedido_de_membresia()
    {
        await using var db = ReportesDb.Nueva();
        var m = ReportesDb.Miembro(db, "AMB-000010");
        ReportesDb.Orden(db, "AMB-000010", new DateTime(2026, 2, 1), (int?)null);   // sin productos de membresia: no cuenta
        ReportesDb.Orden(db, "AMB-000010", new DateTime(2026, 3, 1), 2, 1);         // el primero: VIP y Lifestyle
        ReportesDb.Orden(db, "AMB-000010", new DateTime(2026, 4, 1), 4);            // despues, Turbo: no lo mueve
        await db.SaveChangesAsync();

        await SignupMembershipLevel.RefreshAsync(db, m);

        m.SignupMembershipLevelId.Should().Be(2);
    }

    [Fact]
    public async Task Sin_pedidos_de_membresia_no_hay_nivel()
    {
        await using var db = ReportesDb.Nueva();
        var m = ReportesDb.Miembro(db, "AMB-000011");
        ReportesDb.Orden(db, "AMB-000011", new DateTime(2026, 3, 1), (int?)null);
        await db.SaveChangesAsync();

        await SignupMembershipLevel.RefreshAsync(db, m);

        m.SignupMembershipLevelId.Should().BeNull();
    }

    /// <summary>
    /// Revision del bloque 5 (I6): dos pedidos de membresia con la misma fecha de pedido y de creacion se desempatan por
    /// Id, igual que 02-nivel-de-alta.sql (ORDER BY o.OrderDate, o.CreationDate, o.Id). Sin desempate, el primero que
    /// devolviera la base.
    /// </summary>
    [Fact]
    public async Task Dos_pedidos_con_la_misma_fecha_se_desempatan_por_Id()
    {
        await using var db = ReportesDb.Nueva();
        var m = ReportesDb.Miembro(db, "AMB-000012");
        var fecha = new DateTime(2026, 3, 1);
        // El Turbo se siembra antes, pero su Id va despues.
        var turbo = ReportesDb.Orden(db, "bbbbbbbb-0000-0000-0000-000000000000", "AMB-000012", fecha, 4);
        var vip = ReportesDb.Orden(db, "aaaaaaaa-0000-0000-0000-000000000000", "AMB-000012", fecha, 2);
        turbo.CreationDate = vip.CreationDate = fecha;
        await db.SaveChangesAsync();

        await SignupMembershipLevel.RefreshAsync(db, m);

        m.SignupMembershipLevelId.Should().Be(2, "gana el pedido de Id menor");
    }
}
