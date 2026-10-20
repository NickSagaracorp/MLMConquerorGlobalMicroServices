using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Jobs;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Plan Q, revision del bloque 4: que un miembro tenga nodo de inscripcion es que su nodo vivo vigente (el mas reciente)
/// tenga Node, lo mismo al encolar que al aplicar; sin el, los puntos y los calificados van al primer patrocinador de su
/// cadena con nodo. La cota de rango cuenta solo ascendientes vivos y, en un cambio de estado, llega a 10 por encima del
/// miembro. Todo dentro de una transaccion que se deshace.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerRevisionBloque4Tests
{
    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private sealed class Captura<T> : ILogger<T>
    {
        public readonly List<(LogLevel Nivel, string Texto)> Mensajes = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Mensajes.Add((logLevel, formatter(state, exception)));
    }

    private static async Task<Captura<ApplyMemberStatisticDeltasJob>> AplicarAsync(AppDbContext db)
    {
        var log = new Captura<ApplyMemberStatisticDeltasJob>();
        await new ApplyMemberStatisticDeltasJob(db, new Reloj(), log).ExecuteAsync();
        db.ChangeTracker.Clear();
        return log;
    }

    /// <summary>Una linea de inscripcion con Node, cada uno patrocinado por el anterior, con estadisticas a 0.</summary>
    private static string[] Linea(AppDbContext db, string prefijo, int n)
    {
        var ids = Enumerable.Range(0, n).Select(_ => SqlServerReportes.NuevoId(prefijo)).ToArray();
        for (var i = 0; i < ids.Length; i++)
        {
            ReportesDb.Miembro(db, ids[i], i == 0 ? null : ids[i - 1]);
            ReportesDb.Nodo(db, ids[i], i == 0 ? null : ids[i - 1], ReportesDb.Ruta(ids[..(i + 1)]), i);
            ReportesDb.Puntos(db, ids[i], 0);
        }
        return ids;
    }

    /// <summary>Un nodo vivo sin Node (como los que dejaba el relleno inicial del Plan M a medias).</summary>
    private static GenealogyEntity NodoSinNode(AppDbContext db, string memberId, string padre, string ruta, int nivel)
    {
        var nodo = new GenealogyEntity
        {
            MemberId = memberId, ParentMemberId = padre, HierarchyPath = ruta, Level = nivel, Node = null, CreatedBy = "prueba"
        };
        db.GenealogyTree.Add(nodo);
        return nodo;
    }

    private static int Inscripcion(AppDbContext db, string id) =>
        db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == id).OrderBy(s => s.Id).Select(s => s.EnrollmentPoints).FirstOrDefault();

    private static int Calificados(AppDbContext db, string id) =>
        db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == id).OrderBy(s => s.Id)
            .Select(s => s.EnrollmentQualifiedTeamMembers).FirstOrDefault();

    private static Task<List<string>> EncoladosAsync(AppDbContext db, string[] ids) =>
        db.RankEvaluationQueue.AsNoTracking().Where(q => ids.Contains(q.EvaluateMemberId) && !q.IsProcessed)
            .Select(q => q.EvaluateMemberId).Distinct().ToListAsync();

    [SqlServerFact]
    public async Task Sin_Node_en_su_nodo_vivo_los_puntos_van_a_el_y_a_la_linea_de_su_patrocinador()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("UPDATE MemberStatisticDeltas SET IsApplied = 1 WHERE IsApplied = 0");
        var linea = Linea(db, "RB", 2);
        var m = SqlServerReportes.NuevoId("RB");
        ReportesDb.Miembro(db, m, linea[1]);
        NodoSinNode(db, m, linea[1], ReportesDb.Ruta(linea[0], linea[1], m), 2);
        ReportesDb.Puntos(db, m, 0);
        await db.SaveChangesAsync();
        var seis = await MembershipPointsDeltas.LevelPointsAsync(db, 3);

        ReportesDb.Suscripcion(db, m, 3, MembershipStatus.Active, DateTime.Now);
        await db.SaveChangesAsync();

        (await db.MemberStatisticDeltas.AsNoTracking()
                .Where(d => d.SourceMemberId == m && d.CreatedBy == MembershipPointsDeltas.Actor)
                .Select(d => new { d.MemberId, d.Scope }).ToListAsync())
            .Should().BeEquivalentTo(new[]
            {
                new { MemberId = m, Scope = MemberStatisticDeltaScope.Member },
                new { MemberId = linea[1], Scope = MemberStatisticDeltaScope.SponsorLine }
            }, "sin Node no tiene linea propia: la suya y la de su patrocinador con nodo");
        await AplicarAsync(db);
        new[] { Inscripcion(db, m), Inscripcion(db, linea[1]), Inscripcion(db, linea[0]) }.Should().Equal(seis, seis, seis);
    }

    [SqlServerFact]
    public async Task Con_un_nodo_vivo_mas_reciente_sin_Node_no_se_acredita_la_posicion_vieja()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("UPDATE MemberStatisticDeltas SET IsApplied = 1 WHERE IsApplied = 0");
        var linea = Linea(db, "RB", 2);
        var otra = Linea(db, "RX", 1);
        var m = SqlServerReportes.NuevoId("RB");
        ReportesDb.Miembro(db, m, linea[1]);
        ReportesDb.Puntos(db, m, 0);
        var viejo = ReportesDb.Nodo(db, m, otra[0], ReportesDb.Ruta(otra[0], m), 1);
        await db.SaveChangesAsync();
        viejo.CreationDate = new DateTime(2020, 1, 1);
        await db.SaveChangesAsync();
        NodoSinNode(db, m, linea[1], ReportesDb.Ruta(linea[0], linea[1], m), 2);
        await db.SaveChangesAsync();
        var seis = await MembershipPointsDeltas.LevelPointsAsync(db, 3);

        ReportesDb.Suscripcion(db, m, 3, MembershipStatus.Active, DateTime.Now);
        await db.SaveChangesAsync();
        await AplicarAsync(db);

        new[] { Inscripcion(db, m), Inscripcion(db, linea[1]), Inscripcion(db, linea[0]) }.Should().Equal(seis, seis, seis);
        Inscripcion(db, otra[0]).Should().Be(0, "el nodo vigente es el mas reciente; el viejo no es su posicion");
    }

    [SqlServerFact]
    public async Task Una_fila_de_linea_sin_nodo_se_marca_aplicada_y_avisa_con_su_id()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("UPDATE MemberStatisticDeltas SET IsApplied = 1 WHERE IsApplied = 0");
        var linea = Linea(db, "RB", 1);
        var m = SqlServerReportes.NuevoId("RB");
        ReportesDb.Miembro(db, m, linea[0]);
        NodoSinNode(db, m, linea[0], ReportesDb.Ruta(linea[0], m), 1);
        var huerfana = new MemberStatisticDelta
        {
            MemberId = m, Scope = MemberStatisticDeltaScope.SponsorLine, EnrollmentPointsDelta = 5,
            SourceMemberId = "NUEVO", CreatedBy = "plan-q-prueba", CreationDate = DateTime.Now
        };
        db.MemberStatisticDeltas.Add(huerfana);
        await db.SaveChangesAsync();

        var log = await AplicarAsync(db);

        (await db.MemberStatisticDeltas.AsNoTracking().SingleAsync(d => d.Id == huerfana.Id)).IsApplied.Should().BeTrue();
        Inscripcion(db, m).Should().Be(0);
        log.Mensajes.Where(x => x.Nivel == LogLevel.Warning).Select(x => x.Texto)
            .Should().ContainSingle(t => t.Contains(huerfana.Id.ToString()) && t.Contains(" 1 "));
    }

    [SqlServerFact]
    public async Task Sin_Node_los_calificados_van_al_primer_patrocinador_con_nodo_y_su_linea()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("UPDATE MemberStatisticDeltas SET IsApplied = 1 WHERE IsApplied = 0");
        var linea = Linea(db, "RB", 2);
        var m = SqlServerReportes.NuevoId("RB");
        ReportesDb.Miembro(db, m, linea[1]);
        NodoSinNode(db, m, linea[1], ReportesDb.Ruta(linea[0], linea[1], m), 2);
        ReportesDb.Puntos(db, m, 0);
        await db.SaveChangesAsync();

        var s = ReportesDb.Suscripcion(db, m, 3, MembershipStatus.Active, DateTime.Now);
        await db.SaveChangesAsync();
        await AplicarAsync(db);
        new[] { Calificados(db, m), Calificados(db, linea[1]), Calificados(db, linea[0]) }.Should().Equal(0, 1, 1);

        db.MembershipSubscriptions.Attach(s);
        s.SubscriptionStatus = MembershipStatus.Cancelled;
        await db.SaveChangesAsync();
        await AplicarAsync(db);
        new[] { Calificados(db, m), Calificados(db, linea[1]), Calificados(db, linea[0]) }.Should().Equal(0, 0, 0);
    }

    [SqlServerFact]
    public async Task Una_SponsorLine_solo_de_calificados_no_encola_rango()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("UPDATE MemberStatisticDeltas SET IsApplied = 1 WHERE IsApplied = 0");
        var ids = Linea(db, "RB", 3);
        db.MemberStatisticDeltas.Add(new MemberStatisticDelta
        {
            MemberId = ids[2], Scope = MemberStatisticDeltaScope.SponsorLine, EnrollmentQualifiedTeamMembersDelta = 1,
            SourceMemberId = "NUEVO", CreatedBy = QualifiedTeamDeltas.Actor, CreationDate = DateTime.Now
        });
        await db.SaveChangesAsync();
        await AplicarAsync(db);

        ids.Select(i => Calificados(db, i)).Should().Equal(1, 1, 1);
        (await EncoladosAsync(db, ids)).Should().BeEmpty("el rango no lee los calificados del equipo");
    }
}
