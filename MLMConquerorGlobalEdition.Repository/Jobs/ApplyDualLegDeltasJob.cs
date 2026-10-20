using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.Repository.Jobs;

/// <summary>
/// Plan Q (P-R9): vacia DualLegDeltas cada minuto (LegPointMaintenance.DrainAsync). Un solo vaciador: el applock
/// "dual-leg-deltas" de cada tanda lo excluye de las comisiones que vacian antes de leer.
/// Revision del bloque 2 (I-1): una ejecucion que encuentra otra en marcha se SALTA en el acto (candado de sesion
/// <see cref="RunLockResource"/> con espera 0). Con DisableConcurrentExecution(3600) y un cron por minuto, un vaciado
/// largo apilaba ejecuciones que ocupaban trabajadores de "bizcenter" hasta una hora (AutoPlacementJob, instantanea).
/// Sin reintentos automaticos: la siguiente pasada es dentro de un minuto.
/// Revision 2 del bloque 2 (I1): es el UNICO que aisla un fallo de datos (isolateFailures) y, en cada pasada, registra
/// como error los deltas apartados en DualLegDeltaDeadLetters (debe estar vacia, Q-R30).
/// Vive en Repository por la misma razon que ApplyMemberStatisticDeltasJob: todos los planificadores de Hangfire deben
/// poder cargar su tipo. Corre en la cola "bizcenter", la del servicio que coloca (AutoPlacementJob).
/// </summary>
[Queue("bizcenter")]
public class ApplyDualLegDeltasJob
{
    public const string RunLockResource = "apply-dual-leg-deltas-run";

    private readonly AppDbContext _db;
    private readonly ILogger<ApplyDualLegDeltasJob> _logger;

    public ApplyDualLegDeltasJob(AppDbContext db, ILogger<ApplyDualLegDeltasJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task<int> ExecuteAsync(CancellationToken ct = default)
    {
        var apartados = await LegPointMaintenance.DeadLetterCountAsync(_db, ct);
        if (apartados > 0)
            _logger.LogError("ApplyDualLegDeltas: {Count} deltas de pierna apartados en DualLegDeltaDeadLetters; sus piernas quedan sin ellos hasta reponerlos (Q-R30).",
                apartados);

        if (!_db.Database.IsSqlServer())
            return await LegPointMaintenance.DrainAsync(_db, ct: ct, logger: _logger, isolateFailures: true);

        await _db.Database.OpenConnectionAsync(ct);
        try
        {
            var r = await _db.Database.SqlQuery<int>(
                    $"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {RunLockResource}, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0; SELECT @r AS [Value]")
                .ToListAsync(ct);
            if (r.Count != 1 || r[0] < 0)
            {
                _logger.LogInformation("ApplyDualLegDeltas: otra ejecucion sigue vaciando; esta se salta.");
                return 0;
            }
            try
            {
                var n = await LegPointMaintenance.DrainAsync(_db, ct: ct, logger: _logger, isolateFailures: true);
                if (n > 0) _logger.LogInformation("ApplyDualLegDeltas: {Count} deltas de pierna aplicados.", n);
                return n;
            }
            finally
            {
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"IF APPLOCK_MODE(N'public', {RunLockResource}, N'Session') <> N'NoLock' EXEC sp_releaseapplock @Resource = {RunLockResource}, @LockOwner = 'Session';",
                    CancellationToken.None);
            }
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }
}
