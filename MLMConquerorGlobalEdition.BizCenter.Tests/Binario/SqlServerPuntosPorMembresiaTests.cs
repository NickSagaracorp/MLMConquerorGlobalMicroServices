using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Jobs;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Plan Q (DQ16, reglas de Nick): los puntos del producto cuentan en toda la linea (piernas del binario y EnrollmentPoints de
/// la linea de patrocinio, el propio incluido) solo mientras la membresia esta activa. Entrar en Active suma, salir resta,
/// renovar no cambia nada salvo que cambie el nivel (la diferencia). Se comprueban los dos arboles tras vaciar las dos
/// colas, dentro de una transaccion que se deshace.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerPuntosPorMembresiaTests
{
    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private sealed record Escenario(string Raiz, string Patrocinador, string Miembro);

    /// <summary>
    /// Inscripcion R → P → M (rutas de texto) y binario R ← P ← M por la izquierda; estadisticas a 0. M no se coloca
    /// si <paramref name="colocar"/> es false.
    /// </summary>
    private static async Task<Escenario> SembrarAsync(AppDbContext db, bool colocar = true)
    {
        var x = BinarioDb.Prefijo("PQP");
        var e = new Escenario($"{x}R", $"{x}P", $"{x}M");
        var ahora = DateTime.Now;
        db.GenealogyTree.AddRange(
            new GenealogyEntity { MemberId = e.Raiz, HierarchyPath = $"/{e.Raiz}/", Level = 0, CreatedBy = "plan-q-prueba", CreationDate = ahora },
            new GenealogyEntity { MemberId = e.Patrocinador, ParentMemberId = e.Raiz, HierarchyPath = $"/{e.Raiz}/{e.Patrocinador}/", Level = 1, CreatedBy = "plan-q-prueba", CreationDate = ahora },
            new GenealogyEntity { MemberId = e.Miembro, ParentMemberId = e.Patrocinador, HierarchyPath = $"/{e.Raiz}/{e.Patrocinador}/{e.Miembro}/", Level = 2, CreatedBy = "plan-q-prueba", CreationDate = ahora });
        foreach (var id in new[] { e.Raiz, e.Patrocinador, e.Miembro })
            db.MemberStatistics.Add(new MemberStatisticEntity { MemberId = id, CreatedBy = "plan-q-prueba", CreationDate = ahora });
        await db.SaveChangesAsync();
        await BinarioDb.SembrarPerfilAsync(db, e.Miembro, e.Patrocinador);
        var r = await DualTeamNodes.RootAsync(db, e.Raiz, ahora, "plan-q-prueba");
        db.DualTeamTree.Add(r);
        var p = await DualTeamNodes.ChildAsync(db, r, e.Patrocinador, TreeSide.Left, ahora, "plan-q-prueba");
        db.DualTeamTree.Add(p);
        if (colocar) db.DualTeamTree.Add(await DualTeamNodes.ChildAsync(db, p, e.Miembro, TreeSide.Left, ahora, "plan-q-prueba"));
        await db.SaveChangesAsync();
        return e;
    }

    private static async Task VaciarAsync(AppDbContext db)
    {
        await LegPointMaintenance.DrainAsync(db);
        await new ApplyMemberStatisticDeltasJob(db, new Reloj(), NullLogger<ApplyMemberStatisticDeltasJob>.Instance).ExecuteAsync();
        db.ChangeTracker.Clear();
    }

    /// <summary>Los puntos que ve la linea: (piernas izquierdas de R y de P, EnrollmentPoints de R, P y M, PersonalPoints de M).</summary>
    private static async Task<(decimal R, decimal P, int ER, int EP, int EM, int PM)> LineaAsync(AppDbContext db, Escenario e)
    {
        var r = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == e.Raiz);
        var p = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == e.Patrocinador);
        int E(string id) => db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == id).OrderBy(s => s.Id).Select(s => s.EnrollmentPoints).First();
        var pm = await db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == e.Miembro).OrderBy(s => s.Id).Select(s => s.PersonalPoints).FirstAsync();
        // DualTeamPoints es la copia de las piernas (no lo escribe nadie mas).
        var dtp = await db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == e.Raiz).OrderBy(s => s.Id).Select(s => s.DualTeamPoints).FirstAsync();
        dtp.Should().Be((int)(r.LeftLegPoints + r.RightLegPoints));
        return (r.LeftLegPoints, p.LeftLegPoints, E(e.Raiz), E(e.Patrocinador), E(e.Miembro), pm);
    }

    private static async Task CambiarAsync(AppDbContext db, string subId, Action<MembershipSubscription> cambio)
    {
        cambio(await db.MembershipSubscriptions.SingleAsync(s => s.Id == subId));
        await db.SaveChangesAsync();
        await VaciarAsync(db);
    }

    [SqlServerFact]
    public async Task Alta_cancelacion_retencion_reactivaciones_y_renovacion()
    {
        await using var db = SqlServerReportes.Db();
        db.Database.SetCommandTimeout(300);
        await using var tx = await db.Database.BeginTransactionAsync();
        var e = await SembrarAsync(db);
        var seis = MembershipPointsDeltas.LevelPoints(db, 3);
        seis.Should().BeGreaterThan(0, "el producto del nivel 3 tiene puntos de calificacion");

        var sus = ReportesDb.Suscripcion(db, e.Miembro, 3, MembershipStatus.Pending, DateTime.Now);
        await db.SaveChangesAsync();
        await VaciarAsync(db);
        (await LineaAsync(db, e)).Should().Be((0m, 0m, 0, 0, 0, 0), "pendiente de pago no cuenta");

        await CambiarAsync(db, sus.Id, s => s.SubscriptionStatus = MembershipStatus.Active);
        (await LineaAsync(db, e)).Should().Be((seis, seis, seis, seis, seis, seis), "el alta suma a toda la linea");

        await CambiarAsync(db, sus.Id, s => { s.StartDate = DateTime.Now.AddMonths(1); s.ChangeReason = SubscriptionChangeReason.Renewal; });
        (await LineaAsync(db, e)).Should().Be((seis, seis, seis, seis, seis, seis), "renovar sin cambiar de nivel no cambia nada");

        await CambiarAsync(db, sus.Id, s => s.SubscriptionStatus = MembershipStatus.HoldByBilling);
        (await LineaAsync(db, e)).Should().Be((0m, 0m, 0, 0, 0, 0), "retenida por cobro resta");

        await CambiarAsync(db, sus.Id, s => s.SubscriptionStatus = MembershipStatus.Active);
        (await LineaAsync(db, e)).Should().Be((seis, seis, seis, seis, seis, seis), "reactivar desde retenida suma");

        await CambiarAsync(db, sus.Id, s => s.SubscriptionStatus = MembershipStatus.Cancelled);
        (await LineaAsync(db, e)).Should().Be((0m, 0m, 0, 0, 0, 0), "cancelar resta");

        await CambiarAsync(db, sus.Id, s => s.SubscriptionStatus = MembershipStatus.Active);
        (await LineaAsync(db, e)).Should().Be((seis, seis, seis, seis, seis, seis), "reactivar desde cancelada suma (confirmado por Nick)");

        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Renovar_con_cambio_de_nivel_aplica_la_diferencia()
    {
        await using var db = SqlServerReportes.Db();
        db.Database.SetCommandTimeout(300);
        await using var tx = await db.Database.BeginTransactionAsync();
        var e = await SembrarAsync(db);
        var p3 = MembershipPointsDeltas.LevelPoints(db, 3);
        var p2 = MembershipPointsDeltas.LevelPoints(db, 2);
        p2.Should().NotBe(p3);

        var sus = ReportesDb.Suscripcion(db, e.Miembro, 3, MembershipStatus.Active, DateTime.Now);
        await db.SaveChangesAsync();
        await VaciarAsync(db);
        (await LineaAsync(db, e)).Should().Be((p3, p3, p3, p3, p3, p3));

        // Renovacion que cambia el nivel en la misma fila.
        await CambiarAsync(db, sus.Id, s => { s.MembershipLevelId = 2; s.ChangeReason = SubscriptionChangeReason.Renewal; });
        (await LineaAsync(db, e)).Should().Be((p2, p2, p2, p2, p2, p2), "la diferencia p2 - p3");

        // Subida como la hace UpgradeMembershipHandler: la vieja caduca y nace otra activa, en el mismo guardado.
        var vieja = await db.MembershipSubscriptions.SingleAsync(s => s.Id == sus.Id);
        vieja.SubscriptionStatus = MembershipStatus.Expired;
        ReportesDb.Suscripcion(db, e.Miembro, 3, MembershipStatus.Active, DateTime.Now);
        await db.SaveChangesAsync();
        await VaciarAsync(db);
        (await LineaAsync(db, e)).Should().Be((p3, p3, p3, p3, p3, p3), "la diferencia p3 - p2");

        await tx.RollbackAsync();
    }

    /// <summary>Activado antes de colocarse: su propio de puntos espera y, al colocarlo, sube una sola vez.</summary>
    [SqlServerFact]
    public async Task Activar_antes_de_colocar_cuenta_una_vez()
    {
        await using var db = SqlServerReportes.Db();
        db.Database.SetCommandTimeout(300);
        await using var tx = await db.Database.BeginTransactionAsync();
        var e = await SembrarAsync(db, colocar: false);
        var seis = MembershipPointsDeltas.LevelPoints(db, 3);

        ReportesDb.Suscripcion(db, e.Miembro, 3, MembershipStatus.Active, DateTime.Now);
        await db.SaveChangesAsync();
        await VaciarAsync(db);
        (await db.DualLegDeltas.CountAsync(d => d.StartMemberId == e.Miembro && d.OwnChange && d.Points == seis))
            .Should().Be(1, "sin nodo, el propio de puntos espera");

        var p = await db.DualTeamTree.SingleAsync(d => d.MemberId == e.Patrocinador);
        db.DualTeamTree.Add(await DualTeamNodes.ChildAsync(db, p, e.Miembro, TreeSide.Left, DateTime.Now, "plan-q-prueba"));
        await db.SaveChangesAsync();
        await LegPointMaintenance.EnqueueLeafAsync(db, e.Miembro, DateTime.Now, "plan-q-prueba");
        await VaciarAsync(db);
        var r = await LineaAsync(db, e);
        (r.R, r.P).Should().Be((seis, seis), "la hoja lleva lo aplicado (0) y el propio aparcado los puntos");

        await tx.RollbackAsync();
    }
}
