using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
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
        ReportesDb.Nodo(db, "AMB-Q1", null, "/AMB-Q1/", 0);   // con nodo: su fila es AncestorsOf
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
        await QualifiedTeamDeltas.RecordAsync(db, DateTime.Now);   // como un guardado que fallo y se reintenta
        await QualifiedTeamDeltas.RecordAsync(db, DateTime.Now);
        await db.SaveChangesAsync();                    // el interceptor lo registra una tercera vez
        (await PropiosAsync(db, "AMB-Q1")).Should().Equal(1);
        (await DeLineaAsync(db, "AMB-Q1")).Should().Equal(1);
    }

    // ─── Plan Q (Tarea 19b, DQ13): la fila de la cola de estadisticas para los ascendientes de inscripcion ─────────

    private static Task<List<int>> DeLineaAsync(AppDbContext db, string memberId) =>
        db.MemberStatisticDeltas.AsNoTracking()
            .Where(d => d.MemberId == memberId && d.Scope == MemberStatisticDeltaScope.AncestorsOf)
            .OrderBy(d => d.Id).Select(d => d.EnrollmentQualifiedTeamMembersDelta).ToListAsync();

    [Fact]
    public async Task Activar_y_cancelar_encolan_una_fila_para_los_ascendientes_de_inscripcion()
    {
        var (db, s) = await ConSuscripcionAsync(MembershipStatus.Pending);
        (await DeLineaAsync(db, "AMB-Q1")).Should().BeEmpty();
        s.SubscriptionStatus = MembershipStatus.Active;
        await db.SaveChangesAsync();
        s.SubscriptionStatus = MembershipStatus.Cancelled;
        await db.SaveChangesAsync();
        (await DeLineaAsync(db, "AMB-Q1")).Should().Equal(1, -1);
        (await db.MemberStatisticDeltas.AsNoTracking().Where(d => d.Scope == MemberStatisticDeltaScope.AncestorsOf)
            .Select(d => new { d.EnrollmentPointsDelta, d.EnrollmentTeamSizeDelta, d.QualifiedSponsoredMembersDelta, d.SourceMemberId })
            .ToListAsync()).Should().OnlyContain(d => d.EnrollmentPointsDelta == 0 && d.EnrollmentTeamSizeDelta == 0
                                                     && d.QualifiedSponsoredMembersDelta == 0 && d.SourceMemberId == "AMB-Q1");
    }

    /// <summary>
    /// Revision del bloque 4: sin nodo de inscripcion, la fila va como SponsorLine al primer patrocinador de su cadena con
    /// nodo (el incluido: quedara en su equipo); sin ninguno, no hay fila. Antes era una AncestorsOf que nadie aplicaba.
    /// </summary>
    [Fact]
    public async Task Sin_nodo_la_fila_va_al_primer_patrocinador_con_nodo_y_sin_ninguno_no_hay_fila()
    {
        var db = ReportesDb.Nueva();
        ReportesDb.Miembro(db, "AMB-R");
        ReportesDb.Nodo(db, "AMB-R", null, "/AMB-R/", 0);
        ReportesDb.Miembro(db, "AMB-S", "AMB-R");   // sin nodo
        ReportesDb.Miembro(db, "AMB-M", "AMB-S");   // sin nodo
        ReportesDb.Miembro(db, "AMB-H");            // sin nodo ni patrocinador
        ReportesDb.Suscripcion(db, "AMB-M", 2, MembershipStatus.Active, ReportesDb.Base);
        ReportesDb.Suscripcion(db, "AMB-H", 2, MembershipStatus.Active, ReportesDb.Base);
        await db.SaveChangesAsync();

        (await db.MemberStatisticDeltas.AsNoTracking().Where(d => d.CreatedBy == QualifiedTeamDeltas.Actor)
                .Select(d => new { d.MemberId, d.Scope, d.EnrollmentQualifiedTeamMembersDelta, d.SourceMemberId }).ToListAsync())
            .Should().BeEquivalentTo(new[]
            {
                new { MemberId = "AMB-R", Scope = MemberStatisticDeltaScope.SponsorLine, EnrollmentQualifiedTeamMembersDelta = 1, SourceMemberId = "AMB-M" }
            });
    }
}
