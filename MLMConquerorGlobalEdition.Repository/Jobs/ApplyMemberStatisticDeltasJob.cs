using Hangfire;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Jobs;

/// <summary>
/// Sprint-16 — drains <c>MemberStatisticDeltas</c> into <c>MemberStatistics</c>.
///
/// CompleteSignupHandler enqueues one delta row per ancestor (76 rows for a
/// 76-deep tree) in a single batch insert instead of issuing 76 MERGE
/// round-trips inline. This job runs every minute on the <c>signups</c> queue,
/// claims up to <see cref="BatchSize"/> unapplied rows per inner batch and loops
/// until the whole backlog is drained, groups by <c>MemberId</c> (so N signups
/// under the same upline collapse to one MERGE per cycle), and applies the summed
/// deltas with a race-free MERGE WITH (HOLDLOCK). It then re-enqueues a
/// <c>RankEvaluationQueue</c> row per touched member so the rank processor sees
/// fresh stats on its next tick.
///
/// IMPORTANT — this type lives in the shared Repository assembly ON PURPOSE.
/// Hangfire's RecurringJobScheduler runs on EVERY service's background server and
/// must deserialize the recurring job's type to schedule it. When this class lived
/// in MLMConquerorGlobalEdition.SignupAPI, sibling services (Billing, BizCenter,
/// CommissionEngine, RankEngine, TicketManagementSystem) could not resolve the
/// assembly → JobLoadException → after 5 retries the recurring entry was poisoned
/// (NextExecution=null) and auto-draining silently stopped. Every Hangfire server
/// references Repository, so keeping the job here makes the type loadable by all
/// schedulers. Execution still happens only on the SignupAPI server because the
/// recurring job is enqueued onto the <c>signups</c> queue, which only it listens on.
///
/// Plan Q (Bloque 4): una fila puede ser de un miembro (Scope = Member, las anteriores al Plan Q) o de una linea entera
/// (Scope = SponsorLine: MemberId es el patrocinador y el delta se aplica a el y a cada ascendiente de su Node, con
/// QualifiedSponsoredMembersDelta solo a el). En SQL Server cada tanda es un comando por conjuntos en una transaccion;
/// la cola de rango se llena solo hasta GenealogyUpline.EnrollmentLevels de distancia del patrocinador.
/// Tarea 19b (DQ13): Scope = AncestorsOf (MemberId es quien cambio) aplica EnrollmentQualifiedTeamMembersDelta a cada
/// ascendiente de su Node (distancia 1 en adelante), no a el, y no encola evaluaciones de rango. Cada tanda toma el candado
/// de la cola (member-statistic-deltas) con el que 04-calificados-de-inscripcion.sql la excluye mientras recalcula.
/// Revision del bloque 4: el Node de partida es el del nodo vivo vigente (GenealogySubtree.CurrentNodeAsync, lo mismo que
/// mira quien encola); una SponsorLine que solo lleva calificados (la de quien no tiene nodo, sobre su patrocinador) no
/// encola rango.
/// </summary>
[Queue("signups")]
public class ApplyMemberStatisticDeltasJob
{
    /// <summary>Rows claimed per inner batch. Each cycle loops batches until the queue is
    /// drained (see ExecuteAsync), so this just bounds the per-transaction size.</summary>
    public const int BatchSize = 5000;

    /// <summary>Plan Q (revision C2): reintentos de una tanda si SQL Server la elige victima de un interbloqueo (1205).</summary>
    public const int MaxDeadlockRetries = 3;

    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<ApplyMemberStatisticDeltasJob> _logger;

    public ApplyMemberStatisticDeltasJob(
        AppDbContext db,
        IDateTimeProvider dateTime,
        ILogger<ApplyMemberStatisticDeltasJob> logger)
    {
        _db       = db;
        _dateTime = dateTime;
        _logger   = logger;
    }

    // Prevent the 1-minute recurrence from starting a second drainer while a long backlog
    // drain is still running — overlapping runs would claim the same rows (no row lock) and
    // double-apply the deltas. One drainer at a time is correct and sufficient.
    [DisableConcurrentExecution(timeoutInSeconds: 7200)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var startedAt    = _dateTime.Now;
        var totalApplied = 0;
        while (true)
        {
            var n = _db.Database.IsSqlServer()
                ? await ApplyBatchSqlServerAsync(ct)
                : await ApplyBatchInMemoryAsync(ct);
            if (n == 0) break;
            totalApplied += n;
            _db.ChangeTracker.Clear(); // no acumular entidades seguidas entre tandas
        }

        var elapsedMs = (int)(_dateTime.Now - startedAt).TotalMilliseconds;
        if (totalApplied > 0)
            _logger.LogInformation(
                "ApplyMemberStatisticDeltas: applied {DeltaCount} deltas in {ElapsedMs}ms.",
                totalApplied, elapsedMs);
    }

    /// <summary>
    /// Una tanda en SQL Server: un comando, una transaccion (la de quien llama si ya hay una, en pruebas). El MERGE, la
    /// cola de rango y la marca IsApplied se confirman juntos: o se aplica la tanda entera o los deltas siguen pendientes
    /// (revision C2: sin transaccion, una caida entre el MERGE y la marca volvia a aplicar el MERGE).
    /// </summary>
    private async Task<int> ApplyBatchSqlServerAsync(CancellationToken ct)
    {
        for (var intento = 1; ; intento++)
        {
            var propia = _db.Database.CurrentTransaction is null;
            await using var tx = propia ? await _db.Database.BeginTransactionAsync(ct) : null;
            try
            {
                var r = (await _db.Database.SqlQueryRaw<Tanda>(ApplyBatchSql,
                        new SqlParameter("@lote", BatchSize),
                        new SqlParameter("@now", _dateTime.Now),
                        new SqlParameter("@enrollment", (int)RankEvaluationTrigger.Enrollment),
                        new SqlParameter("@niveles", GenealogyUpline.EnrollmentLevels))
                    .ToListAsync(ct)).Single();
                if (tx is not null) await tx.CommitAsync(ct);
                AvisarSinNodo(r.SinNodo, r.IdsSinNodo);
                return r.Aplicadas;
            }
            catch (SqlException ex) when (ex.Number == 1205 && propia && intento < MaxDeadlockRetries)
            {
                _logger.LogWarning(ex, "ApplyMemberStatisticDeltas: tanda victima de un interbloqueo, intento {Intento}.", intento);
            }
        }
    }

    /// <summary>Lo que devuelve una tanda en SQL Server.</summary>
    private sealed class Tanda
    {
        public int Aplicadas { get; set; }
        public int SinNodo { get; set; }
        public string? IdsSinNodo { get; set; }
    }

    /// <summary>
    /// Revision del bloque 4: una fila de linea (SponsorLine o AncestorsOf) cuyo miembro no tiene nodo vigente con Node se
    /// marca aplicada sin acreditar a nadie y se avisa con cuantas y sus Id (las 100 primeras). Desde que al encolar se mira
    /// lo mismo (GenealogySubtree.CurrentNodeAsync), solo pasa si el nodo cambia entre encolar y aplicar; la fila sigue en
    /// MemberStatisticDeltas para volver a encolarla a mano. No se deja pendiente: cada tanda la volveria a tomar sin fin.
    /// </summary>
    private void AvisarSinNodo(int sinNodo, string? ids)
    {
        if (sinNodo > 0)
            _logger.LogWarning(
                "ApplyMemberStatisticDeltas: {SinNodo} filas de linea sin nodo vigente con Node, marcadas aplicadas sin acreditar a nadie. Ids: {Ids}",
                sinNodo, ids);
    }

    /// <summary>
    /// Si una fila encola evaluaciones de rango (la columna Rango del SQL): las de miembro y las SponsorLine que mueven
    /// puntos, equipo o patrocinados; no las AncestorsOf ni la SponsorLine de respaldo de los calificados (solo QQ).
    /// </summary>
    private static bool EncolaRango(MemberStatisticDelta d) =>
        d.Scope == MemberStatisticDeltaScope.Member
        || (d.Scope == MemberStatisticDeltaScope.SponsorLine
            && (d.EnrollmentPointsDelta != 0 || d.EnrollmentTeamSizeDelta != 0 || d.QualifiedSponsoredMembersDelta != 0));

    /// <summary>
    /// Una tanda en InMemory (pruebas). SponsorLine sube por ParentMemberId del nodo vigente de cada miembro (las pruebas
    /// siembran nodos sin Node, revision I6); el patrocinador sin nodo no recibe nada, como en SQL.
    /// </summary>
    private async Task<int> ApplyBatchInMemoryAsync(CancellationToken ct)
    {
        var batch = await _db.MemberStatisticDeltas.Where(d => !d.IsApplied).OrderBy(d => d.Id).Take(BatchSize).ToListAsync(ct);
        if (batch.Count == 0) return 0;
        var now = _dateTime.Now;

        var filas = new List<(string MemberId, int Distancia, MemberStatisticDelta Delta)>();
        foreach (var d in batch)
        {
            if (d.Scope == MemberStatisticDeltaScope.Member)
            {
                filas.Add((d.MemberId, 0, d));
                continue;
            }
            var vistos = new HashSet<string>(StringComparer.Ordinal);
            var distancia = 0;
            for (var actual = d.MemberId; actual is not null && vistos.Add(actual); distancia++)
            {
                var id = actual;
                var nodo = await _db.GenealogyTree.AsNoTracking().Where(g => g.MemberId == id)
                    .OrderByDescending(g => g.CreationDate).ThenByDescending(g => g.Id)
                    .Select(g => new { g.ParentMemberId }).FirstOrDefaultAsync(ct);
                if (nodo is null) break;
                if (d.Scope != MemberStatisticDeltaScope.AncestorsOf || distancia > 0) filas.Add((id, distancia, d));
                actual = nodo.ParentMemberId;
            }
        }

        var grupos = filas.GroupBy(f => f.MemberId).Select(g => new
        {
            MemberId  = g.Key,
            P         = g.Sum(f => f.Delta.EnrollmentPointsDelta),
            T         = g.Sum(f => f.Delta.EnrollmentTeamSizeDelta),
            Q         = g.Sum(f => f.Delta.Scope == MemberStatisticDeltaScope.Member || f.Distancia == 0
                                   ? f.Delta.QualifiedSponsoredMembersDelta : 0),
            QQ        = g.Sum(f => f.Delta.EnrollmentQualifiedTeamMembersDelta),
            CreatedBy = g.OrderBy(f => f.Delta.Id).First().Delta.CreatedBy,
            // La evaluacion de rango no lee los calificados del equipo: las filas solo de calificados no encolan.
            Encolar   = g.Any(f => EncolaRango(f.Delta) && f.Distancia < GenealogyUpline.EnrollmentLevels
                                   + (string.Equals(f.Delta.SourceMemberId, f.Delta.MemberId, StringComparison.Ordinal) ? 1 : 0))
        }).ToList();

        foreach (var grp in grupos)
        {
            var existing = await _db.MemberStatistics.FirstOrDefaultAsync(s => s.MemberId == grp.MemberId, ct);
            if (existing is not null)
            {
                existing.EnrollmentPoints          += grp.P;
                existing.EnrollmentTeamSize        += grp.T;
                existing.QualifiedSponsoredMembers += grp.Q;
                existing.EnrollmentQualifiedTeamMembers += grp.QQ;
            }
            else
            {
                await _db.MemberStatistics.AddAsync(new MemberStatisticEntity
                {
                    MemberId = grp.MemberId, EnrollmentPoints = grp.P, EnrollmentTeamSize = grp.T,
                    QualifiedSponsoredMembers = grp.Q, EnrollmentQualifiedTeamMembers = grp.QQ,
                    CreatedBy = grp.CreatedBy, CreationDate = now
                }, ct);
            }
        }

        foreach (var d in batch)
        {
            d.IsApplied = true;
            d.AppliedAt = now;
        }

        var tocados = grupos.Where(g => g.Encolar).Select(g => g.MemberId).ToList();
        var yaEncolados = (await _db.RankEvaluationQueue
                .Where(q => !q.IsProcessed && q.TriggerEvent == RankEvaluationTrigger.Enrollment && tocados.Contains(q.EvaluateMemberId))
                .Select(q => q.EvaluateMemberId).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var id in tocados.Where(i => !yaEncolados.Contains(i)))
            await _db.RankEvaluationQueue.AddAsync(new RankEvaluationQueue
            {
                TriggerMemberId = id, EvaluateMemberId = id, TriggerEvent = RankEvaluationTrigger.Enrollment,
                TriggerDate = now, CreatedBy = "delta-apply", CreationDate = now
            }, ct);

        await _db.SaveChangesAsync(ct);
        return batch.Count;
    }

    private const string ApplyBatchSql = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @candado int;
EXEC @candado = sp_getapplock @Resource = N'member-statistic-deltas', @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 30000;
IF @candado < 0
    THROW 51115, N'La cola de estadisticas esta ocupada (member-statistic-deltas): un recalculo en curso.', 1;
DROP TABLE IF EXISTS #lote; DROP TABLE IF EXISTS #exp; DROP TABLE IF EXISTS #agg;
CREATE TABLE #lote (Id bigint PRIMARY KEY, MemberId nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL, Scope int NOT NULL,
                    P int NOT NULL, T int NOT NULL, Q int NOT NULL, QQ int NOT NULL,
                    CreatedBy nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL, Node hierarchyid NULL,
                    Propio bit NOT NULL);
-- Revision del bloque 4: el Node de partida de una fila de linea es el de su nodo vivo VIGENTE (el mas reciente, como
-- GenealogySubtree.CurrentOfAsync y CurrentNodeAsync al encolar); si ese no tiene Node, no hay linea. Antes se tomaba el
-- mas reciente CON Node: un vigente sin Node acreditaba una posicion vieja.
INSERT INTO #lote (Id, MemberId, Scope, P, T, Q, QQ, CreatedBy, Node, Propio)
SELECT d.Id, d.MemberId, d.Scope, d.EnrollmentPointsDelta, d.EnrollmentTeamSizeDelta, d.QualifiedSponsoredMembersDelta,
       d.EnrollmentQualifiedTeamMembersDelta, d.CreatedBy, v.Node, CASE WHEN d.SourceMemberId = d.MemberId THEN 1 ELSE 0 END
FROM (SELECT TOP (@lote) Id, MemberId, Scope, EnrollmentPointsDelta, EnrollmentTeamSizeDelta, QualifiedSponsoredMembersDelta,
             EnrollmentQualifiedTeamMembersDelta, CreatedBy, SourceMemberId
      FROM MemberStatisticDeltas WHERE IsApplied = 0 ORDER BY Id) d
OUTER APPLY (SELECT TOP 1 g.Node FROM GenealogyTree g
             WHERE d.Scope <> 0 AND g.MemberId = d.MemberId AND g.IsDeleted = 0
             ORDER BY g.CreationDate DESC, g.Id DESC) v;

-- Scope 0: la fila es del propio MemberId. Scope 1 (SponsorLine): MemberId es el patrocinador y el delta va a el y a cada
-- ascendiente de su Node vigente. Scope 2 (AncestorsOf): MemberId es quien cambio y el delta va a sus ascendientes
-- (Distancia >= 1), no a el.
SELECT x.MemberId, x.Distancia, l.Scope, l.P, l.T,
       CASE WHEN l.Scope = 0 OR x.Distancia = 0 THEN l.Q ELSE 0 END AS Q, l.QQ, l.CreatedBy,
       -- Encola rango: las filas de miembro y las SponsorLine que mueven algo que lee el rango (no la de respaldo de los
       -- calificados, que solo lleva QQ); nunca las AncestorsOf.
       CASE WHEN l.Scope = 0 OR (l.Scope = 1 AND (l.P <> 0 OR l.T <> 0 OR l.Q <> 0)) THEN 1 ELSE 0 END AS Rango, l.Propio
INTO #exp
FROM #lote l
CROSS APPLY (
    SELECT l.MemberId AS MemberId, 0 AS Distancia WHERE l.Scope = 0
    UNION ALL
    SELECT a.MemberId, an.Distancia
    FROM dbo.GenealogyAncestors(l.Node) an
    JOIN GenealogyTree a ON a.Node = an.Node AND a.IsDeleted = 0
    WHERE l.Node IS NOT NULL AND (l.Scope = 1 OR (l.Scope = 2 AND an.Distancia >= 1))
) x;

-- Filas de linea cuyo miembro no tiene nodo vigente con Node: no hay a quien aplicarlas. Se marcan aplicadas como las
-- demas (pendientes, cada tanda las volveria a tomar sin fin), pero se cuentan y se devuelven sus Id para el aviso; la
-- fila sigue en MemberStatisticDeltas. Una AncestorsOf de una raiz (con Node y sin ascendientes) no cuenta.
DECLARE @sinNodo int = (SELECT COUNT(*) FROM #lote WHERE Scope <> 0 AND Node IS NULL);
DECLARE @idsSinNodo nvarchar(max) =
    (SELECT STRING_AGG(CAST(Id AS nvarchar(max)), N',') WITHIN GROUP (ORDER BY Id)
     FROM (SELECT TOP (100) Id FROM #lote WHERE Scope <> 0 AND Node IS NULL ORDER BY Id) t);

SELECT MemberId, SUM(P) AS P, SUM(T) AS T, SUM(Q) AS Q, SUM(QQ) AS QQ, MIN(CreatedBy) AS CreatedBy
INTO #agg FROM #exp GROUP BY MemberId;

MERGE INTO MemberStatistics WITH (HOLDLOCK) AS t
USING #agg AS s ON t.MemberId = s.MemberId
WHEN MATCHED THEN UPDATE SET
    EnrollmentPoints               = t.EnrollmentPoints               + s.P,
    EnrollmentTeamSize             = t.EnrollmentTeamSize             + s.T,
    QualifiedSponsoredMembers      = t.QualifiedSponsoredMembers      + s.Q,
    EnrollmentQualifiedTeamMembers = t.EnrollmentQualifiedTeamMembers + s.QQ
WHEN NOT MATCHED THEN INSERT (MemberId, PersonalPoints, ExternalCustomerPoints, DualTeamSize,
        EnrollmentTeamSize, DualTeamPoints, EnrollmentPoints, QualifiedSponsoredMembers, QualifiedSponsoredExternalCustomers,
        EnrollmentQualifiedTeamMembers,
        EnrollmentTeamGrowth, DualteamGrowth, EnrollmentTeamPointsGrowth, DualTeamPointsGrowth,
        CurrentWeekIncomeGrowth, CurrentMonthIncomeGrowth, CurrentYearIncomeGrowth, CreationDate, CreatedBy)
    VALUES (s.MemberId, 0, 0, 0, s.T, 0, s.P, s.Q, 0, s.QQ, 0, 0, 0, 0, 0, 0, 0, @now, s.CreatedBy);

-- Evaluacion de rango inmediata acotada (DQ5), y nunca por filas solo de calificados (Rango = 0): el rango no lee los
-- calificados del equipo. La cota son 10 por encima de quien cambio (revision del bloque 4, Q1): en un alta, la fila va
-- sobre el patrocinador (Distancia < @niveles: el y sus 9, los 10 por encima del nuevo); en una fila propia (SourceMemberId
-- = MemberId: los puntos de una activacion, cancelacion, retencion o reactivacion), el propio y sus @niveles ascendientes.
INSERT INTO RankEvaluationQueue (TriggerMemberId, EvaluateMemberId, TriggerEvent, TriggerDate, IsProcessed, ProcessedAt,
                                 ProcessedBy, ErrorMessage, RetryCount, CreationDate, CreatedBy)
SELECT e.MemberId, e.MemberId, @enrollment, @now, 0, NULL, NULL, NULL, 0, @now, N'delta-apply'
FROM (SELECT DISTINCT MemberId FROM #exp WHERE Rango = 1 AND Distancia < @niveles + Propio) e
WHERE NOT EXISTS (SELECT 1 FROM RankEvaluationQueue q
                  WHERE q.IsProcessed = 0 AND q.TriggerEvent = @enrollment AND q.EvaluateMemberId = e.MemberId);

UPDATE d SET IsApplied = 1, AppliedAt = @now FROM MemberStatisticDeltas d JOIN #lote l ON l.Id = d.Id;
SELECT COUNT(*) AS Aplicadas, @sinNodo AS SinNodo, @idsSinNodo AS IdsSinNodo FROM #lote;
DROP TABLE #agg; DROP TABLE #exp; DROP TABLE #lote;";
}
