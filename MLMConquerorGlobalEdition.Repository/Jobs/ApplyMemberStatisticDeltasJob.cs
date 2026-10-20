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
                var n = (await _db.Database.SqlQueryRaw<int>(ApplyBatchSql,
                        new SqlParameter("@lote", BatchSize),
                        new SqlParameter("@now", _dateTime.Now),
                        new SqlParameter("@enrollment", (int)RankEvaluationTrigger.Enrollment),
                        new SqlParameter("@niveles", GenealogyUpline.EnrollmentLevels))
                    .ToListAsync(ct)).Single();
                if (tx is not null) await tx.CommitAsync(ct);
                return n;
            }
            catch (SqlException ex) when (ex.Number == 1205 && propia && intento < MaxDeadlockRetries)
            {
                _logger.LogWarning(ex, "ApplyMemberStatisticDeltas: tanda victima de un interbloqueo, intento {Intento}.", intento);
            }
        }
    }

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
                filas.Add((id, distancia, d));
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
            CreatedBy = g.OrderBy(f => f.Delta.Id).First().Delta.CreatedBy,
            Encolar   = g.Any(f => f.Distancia < GenealogyUpline.EnrollmentLevels)
        }).ToList();

        foreach (var grp in grupos)
        {
            var existing = await _db.MemberStatistics.FirstOrDefaultAsync(s => s.MemberId == grp.MemberId, ct);
            if (existing is not null)
            {
                existing.EnrollmentPoints          += grp.P;
                existing.EnrollmentTeamSize        += grp.T;
                existing.QualifiedSponsoredMembers += grp.Q;
            }
            else
            {
                await _db.MemberStatistics.AddAsync(new MemberStatisticEntity
                {
                    MemberId = grp.MemberId, EnrollmentPoints = grp.P, EnrollmentTeamSize = grp.T,
                    QualifiedSponsoredMembers = grp.Q, CreatedBy = grp.CreatedBy, CreationDate = now
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
DROP TABLE IF EXISTS #lote; DROP TABLE IF EXISTS #exp; DROP TABLE IF EXISTS #agg;
CREATE TABLE #lote (Id bigint PRIMARY KEY, MemberId nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL, Scope int NOT NULL,
                    P int NOT NULL, T int NOT NULL, Q int NOT NULL, CreatedBy nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL);
INSERT INTO #lote
SELECT TOP (@lote) Id, MemberId, Scope, EnrollmentPointsDelta, EnrollmentTeamSizeDelta, QualifiedSponsoredMembersDelta, CreatedBy
FROM MemberStatisticDeltas WHERE IsApplied = 0 ORDER BY Id;

-- Scope 0: la fila es del propio MemberId (filas anteriores al Plan Q). Scope 1: MemberId es el patrocinador y el delta
-- sube por los ascendientes de su Node vigente (el mismo criterio que CurrentOfAsync).
SELECT x.MemberId, x.Distancia, l.P, l.T,
       CASE WHEN l.Scope = 0 OR x.Distancia = 0 THEN l.Q ELSE 0 END AS Q, l.CreatedBy
INTO #exp
FROM #lote l
CROSS APPLY (
    SELECT l.MemberId AS MemberId, 0 AS Distancia WHERE l.Scope = 0
    UNION ALL
    SELECT a.MemberId, an.Distancia
    FROM (SELECT TOP 1 g.Node FROM GenealogyTree g
          WHERE g.MemberId = l.MemberId AND g.IsDeleted = 0 AND g.Node IS NOT NULL
          ORDER BY g.CreationDate DESC, g.Id DESC) s
    CROSS APPLY dbo.GenealogyAncestors(s.Node) an
    JOIN GenealogyTree a ON a.Node = an.Node AND a.IsDeleted = 0
    WHERE l.Scope = 1
) x;

SELECT MemberId, SUM(P) AS P, SUM(T) AS T, SUM(Q) AS Q, MIN(CreatedBy) AS CreatedBy
INTO #agg FROM #exp GROUP BY MemberId;

MERGE INTO MemberStatistics WITH (HOLDLOCK) AS t
USING #agg AS s ON t.MemberId = s.MemberId
WHEN MATCHED THEN UPDATE SET
    EnrollmentPoints          = t.EnrollmentPoints          + s.P,
    EnrollmentTeamSize        = t.EnrollmentTeamSize        + s.T,
    QualifiedSponsoredMembers = t.QualifiedSponsoredMembers + s.Q
WHEN NOT MATCHED THEN INSERT (MemberId, PersonalPoints, ExternalCustomerPoints, DualTeamSize,
        EnrollmentTeamSize, DualTeamPoints, EnrollmentPoints, QualifiedSponsoredMembers, QualifiedSponsoredExternalCustomers,
        EnrollmentTeamGrowth, DualteamGrowth, EnrollmentTeamPointsGrowth, DualTeamPointsGrowth,
        CurrentWeekIncomeGrowth, CurrentMonthIncomeGrowth, CurrentYearIncomeGrowth, CreationDate, CreatedBy)
    VALUES (s.MemberId, 0, 0, 0, s.T, 0, s.P, s.Q, 0, 0, 0, 0, 0, 0, 0, 0, @now, s.CreatedBy);

-- Evaluacion de rango inmediata solo hasta @niveles de distancia del patrocinador (DQ5).
INSERT INTO RankEvaluationQueue (TriggerMemberId, EvaluateMemberId, TriggerEvent, TriggerDate, IsProcessed, ProcessedAt,
                                 ProcessedBy, ErrorMessage, RetryCount, CreationDate, CreatedBy)
SELECT e.MemberId, e.MemberId, @enrollment, @now, 0, NULL, NULL, NULL, 0, @now, N'delta-apply'
FROM (SELECT DISTINCT MemberId FROM #exp WHERE Distancia < @niveles) e
WHERE NOT EXISTS (SELECT 1 FROM RankEvaluationQueue q
                  WHERE q.IsProcessed = 0 AND q.TriggerEvent = @enrollment AND q.EvaluateMemberId = e.MemberId);

UPDATE d SET IsApplied = 1, AppliedAt = @now FROM MemberStatisticDeltas d JOIN #lote l ON l.Id = d.Id;
SELECT COUNT(*) AS [Value] FROM #lote;
DROP TABLE #agg; DROP TABLE #exp; DROP TABLE #lote;";
}
