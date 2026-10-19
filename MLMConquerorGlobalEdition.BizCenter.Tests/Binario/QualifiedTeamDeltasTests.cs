using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>Plan Q (DQ13): cada cambio de una suscripcion hacia o desde Active encola un +-1 propio del miembro al guardar.</summary>
public class QualifiedTeamDeltasTests
{
    private static async Task<(AppDbContext Db, MembershipSubscription S)> ConSuscripcionAsync(MembershipStatus estado)
    {
        var db = ReportesDb.Nueva();
        ReportesDb.Miembro(db, "AMB-Q1");
        var s = ReportesDb.Suscripcion(db, "AMB-Q1", 2, estado, ReportesDb.Base);
        await db.SaveChangesAsync();
        return (db, s);
    }

    private static Task<List<int>> PropiosAsync(AppDbContext db, string memberId) =>
        db.DualLegDeltas.AsNoTracking().Where(d => d.StartMemberId == memberId && d.OwnChange)
            .OrderBy(d => d.Id).Select(d => d.QualifiedCount).ToListAsync();

    [Fact]
    public async Task Crear_una_activa_encola_mas_uno_y_una_pendiente_nada()
    {
        var (db, _) = await ConSuscripcionAsync(MembershipStatus.Active);
        (await PropiosAsync(db, "AMB-Q1")).Should().Equal(1);
        var (db2, _) = await ConSuscripcionAsync(MembershipStatus.Pending);
        (await PropiosAsync(db2, "AMB-Q1")).Should().BeEmpty();
    }

    [Theory]
    [InlineData(MembershipStatus.Pending, MembershipStatus.Active, 1)]          // activacion del alta o del admin
    [InlineData(MembershipStatus.HoldByBilling, MembershipStatus.Active, 1)]    // reactivacion por cobro
    [InlineData(MembershipStatus.Active, MembershipStatus.Cancelled, -1)]       // cancelacion
    [InlineData(MembershipStatus.Active, MembershipStatus.HoldByBilling, -1)]   // 90 dias sin cobro
    [InlineData(MembershipStatus.Active, MembershipStatus.Expired, -1)]
    [InlineData(MembershipStatus.Cancelled, MembershipStatus.Expired, 0)]
    public async Task Un_cambio_de_estado_encola_su_signo(MembershipStatus de, MembershipStatus a, int esperado)
    {
        var (db, s) = await ConSuscripcionAsync(de);
        var antes = (await PropiosAsync(db, "AMB-Q1")).Count;
        s.SubscriptionStatus = a;
        await db.SaveChangesAsync();
        var nuevos = (await PropiosAsync(db, "AMB-Q1")).Skip(antes).ToList();
        if (esperado == 0) nuevos.Should().BeEmpty(); else nuevos.Should().Equal(esperado);
    }

    [Fact]
    public async Task Subir_de_nivel_en_un_guardado_es_neto_cero()
    {
        var (db, vieja) = await ConSuscripcionAsync(MembershipStatus.Active);
        vieja.SubscriptionStatus = MembershipStatus.Expired;                       // UpgradeMembershipHandler.cs:65
        ReportesDb.Suscripcion(db, "AMB-Q1", 3, MembershipStatus.Active, ReportesDb.Base);   // :70-76
        await db.SaveChangesAsync();
        (await PropiosAsync(db, "AMB-Q1")).Should().Equal(1);   // solo el +1 de la primera alta
    }

    [Fact]
    public async Task Repetir_el_registro_en_el_mismo_guardado_no_duplica()
    {
        var (db, s) = await ConSuscripcionAsync(MembershipStatus.Pending);
        s.SubscriptionStatus = MembershipStatus.Active;
        QualifiedTeamDeltas.Record(db, DateTime.Now);   // como un guardado que fallo y se reintenta
        QualifiedTeamDeltas.Record(db, DateTime.Now);
        await db.SaveChangesAsync();                    // el interceptor lo registra una tercera vez
        (await PropiosAsync(db, "AMB-Q1")).Should().Equal(1);
    }
}
