using Microsoft.Data.SqlClient;
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
/// Plan Q (tercera revision del bloque 2, I-a e I-b): los puntos de membresia (DQ16) con guardados que se cruzan.
/// <list type="bullet">
/// <item>I-a: dos guardados que tocan al mismo miembro calculaban el cambio con lecturas viejas (dos activaciones veian
/// cada una a la otra inactiva; dos cancelaciones de la misma suscripcion restaban las dos). Una tercera conexion retiene
/// la fila de estadisticas del miembro para que los dos calculen antes de que ninguno escriba (bajo RCSI las lecturas no
/// esperan) y la suelta despues: el resultado debe ser el de hacerlos uno detras de otro.</item>
/// <item>I-b: los EnrollmentPoints del propio se escribian como valor absoluto desde una fila seguida vieja y pisaban el
/// MERGE relativo del trabajo de estadisticas.</item>
/// </list>
/// Las de carrera van sin transaccion envolvente (cada contexto confirma) y limpian por prefijo; los servicios -run vacian
/// las colas a la vez, asi que se comprueba lo encolado (MemberStatisticDeltas no se borra al aplicarse) y el binario como
/// pierna aplicada mas lo pendiente, en una sola lectura.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerPuntosConcurrentesTests
{
    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private sealed record Escenario(string Raiz, string Patrocinador, string Miembro);

    /// <summary>Inscripcion R → P → M y binario R ← P ← M por la izquierda; estadisticas a 0.</summary>
    private static async Task<Escenario> SembrarAsync(AppDbContext db, string x)
    {
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
        db.DualTeamTree.Add(await DualTeamNodes.ChildAsync(db, p, e.Miembro, TreeSide.Left, ahora, "plan-q-prueba"));
        await db.SaveChangesAsync();
        return e;
    }

    /// <summary>Lo que ve la linea de M, lo aplicado mas lo pendiente: (PersonalPoints de M, lo encolado a P y a R desde M, pierna de P mas los propios pendientes de M).</summary>
    private static async Task<(int PM, int AP, int AR, decimal PiernaP)> LineaAsync(Escenario e)
    {
        await using var db = SqlServerReportes.Db();
        var pm = await db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == e.Miembro).OrderBy(s => s.Id).Select(s => s.PersonalPoints).FirstAsync();
        int Encolado(string a) => db.MemberStatisticDeltas.AsNoTracking()
            .Where(d => d.SourceMemberId == e.Miembro && d.MemberId == a).Sum(d => d.EnrollmentPointsDelta);
        // Una sola sentencia: el vaciado de cada minuto mueve lo pendiente a la pierna en su transaccion.
        var pierna = (await db.Database.SqlQuery<decimal>($@"
SELECT (SELECT LeftLegPoints FROM DualTeamTree WHERE MemberId = {e.Patrocinador} AND IsDeleted = 0)
     + ISNULL((SELECT SUM(Points) FROM DualLegDeltas WHERE StartMemberId = {e.Miembro} AND OwnChange = 1), 0) AS [Value]").ToListAsync()).Single();
        return (pm, Encolado(e.Patrocinador), Encolado(e.Raiz), pierna);
    }

    /// <summary>
    /// Retiene la fila de estadisticas de M en otra conexion, lanza los dos guardados, espera a que los dos esten parados y
    /// la suelta.
    /// </summary>
    private static async Task CruzarAsync(Escenario e, Func<AppDbContext, Task> a, Func<AppDbContext, Task> b)
    {
        await using var ca = SqlServerReportes.Db();
        await using var cb = SqlServerReportes.Db();
        await a(ca);
        await b(cb);
        await using var retiene = new SqlConnection(SqlServerReportes.ConnectionString);
        await retiene.OpenAsync();
        var tx = (SqlTransaction)await retiene.BeginTransactionAsync();
        await using (var cmd = new SqlCommand("UPDATE MemberStatistics SET PersonalPoints = PersonalPoints WHERE MemberId = @m", retiene, tx))
        {
            cmd.Parameters.AddWithValue("@m", e.Miembro);
            await cmd.ExecuteNonQueryAsync();
        }
        var ga = ca.SaveChangesAsync();
        var gb = cb.SaveChangesAsync();
        await Task.Delay(2000);
        ga.IsCompleted.Should().BeFalse("el guardado espera a la fila retenida");
        gb.IsCompleted.Should().BeFalse("el guardado espera a la fila retenida");
        await tx.RollbackAsync();
        await Task.WhenAll(ga, gb);
    }

    [SqlServerFact]
    public async Task Dos_activaciones_a_la_vez_del_mismo_miembro_suman_las_dos()
    {
        var x = BinarioDb.Prefijo("PQC");
        try
        {
            string s1, s2;
            Escenario e;
            await using (var db = SqlServerReportes.Db())
            {
                e = await SembrarAsync(db, x);
                s1 = ReportesDb.Suscripcion(db, e.Miembro, 3, MembershipStatus.Pending, DateTime.Now).Id;
                s2 = ReportesDb.Suscripcion(db, e.Miembro, 3, MembershipStatus.Pending, DateTime.Now).Id;
                await db.SaveChangesAsync();
            }
            int seis;
            await using (var db = SqlServerReportes.Db()) seis = await MembershipPointsDeltas.LevelPointsAsync(db, 3);

            await CruzarAsync(e,
                async c => (await c.MembershipSubscriptions.SingleAsync(s => s.Id == s1)).SubscriptionStatus = MembershipStatus.Active,
                async c => (await c.MembershipSubscriptions.SingleAsync(s => s.Id == s2)).SubscriptionStatus = MembershipStatus.Active);

            (await LineaAsync(e)).Should().Be((2 * seis, 2 * seis, 2 * seis, 2m * seis), "las dos activas cuentan");
        }
        finally { await BinarioDb.LimpiarAsync(x); }
    }

    [SqlServerFact]
    public async Task Dos_cancelaciones_a_la_vez_de_la_misma_suscripcion_restan_una_vez()
    {
        var x = BinarioDb.Prefijo("PQC");
        try
        {
            string s1;
            Escenario e;
            await using (var db = SqlServerReportes.Db())
            {
                e = await SembrarAsync(db, x);
                var sus = ReportesDb.Suscripcion(db, e.Miembro, 3, MembershipStatus.Pending, DateTime.Now);
                await db.SaveChangesAsync();
                s1 = sus.Id;
                sus.SubscriptionStatus = MembershipStatus.Active;
                await db.SaveChangesAsync();
            }

            await CruzarAsync(e,
                async c => (await c.MembershipSubscriptions.SingleAsync(s => s.Id == s1)).SubscriptionStatus = MembershipStatus.Cancelled,
                async c => (await c.MembershipSubscriptions.SingleAsync(s => s.Id == s1)).SubscriptionStatus = MembershipStatus.Cancelled);

            (await LineaAsync(e)).Should().Be((0, 0, 0, 0m), "la alta suma una vez y la cancelacion resta una vez");
        }
        finally { await BinarioDb.LimpiarAsync(x); }
    }

    [SqlServerFact]
    public async Task Una_fila_de_estadisticas_seguida_y_vieja_no_pisa_los_EnrollmentPoints_que_aplico_el_trabajo()
    {
        await using var db = SqlServerReportes.Db();
        db.Database.SetCommandTimeout(300);
        await using var tx = await db.Database.BeginTransactionAsync();
        var e = await SembrarAsync(db, BinarioDb.Prefijo("PQC"));
        var seis = await MembershipPointsDeltas.LevelPointsAsync(db, 3);
        var sus = ReportesDb.Suscripcion(db, e.Miembro, 3, MembershipStatus.Pending, DateTime.Now);
        await db.SaveChangesAsync();

        // La fila de M seguida desde antes (EnrollmentPoints 0) y el trabajo de estadisticas le suma 5 de un descendiente.
        var fila = await db.MemberStatistics.SingleAsync(s => s.MemberId == e.Miembro);
        fila.EnrollmentPoints.Should().Be(0);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE MemberStatistics SET EnrollmentPoints = EnrollmentPoints + 5 WHERE MemberId = {e.Miembro}");

        sus.SubscriptionStatus = MembershipStatus.Active;
        await db.SaveChangesAsync();
        await LegPointMaintenance.DrainAsync(db);
        await new ApplyMemberStatisticDeltasJob(db, new Reloj(), NullLogger<ApplyMemberStatisticDeltasJob>.Instance).ExecuteAsync();

        var em = await db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == e.Miembro).Select(s => new { s.EnrollmentPoints, s.PersonalPoints }).SingleAsync();
        em.EnrollmentPoints.Should().Be(5 + seis, "lo del trabajo se conserva y el alta suma lo suyo");
        em.PersonalPoints.Should().Be(seis);
    }
}
