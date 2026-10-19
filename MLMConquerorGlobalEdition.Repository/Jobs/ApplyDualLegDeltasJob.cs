using Hangfire;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.Repository.Jobs;

/// <summary>
/// Plan Q (P-R9): vacia DualLegDeltas cada minuto (LegPointMaintenance.DrainAsync). Un solo vaciador: el atributo evita
/// dos ejecuciones del trabajo, y el applock "dual-leg-deltas" lo excluye de las comisiones que vacian antes de leer.
/// Vive en Repository por la misma razon que ApplyMemberStatisticDeltasJob: todos los planificadores de Hangfire deben
/// poder cargar su tipo. Corre en la cola "bizcenter", la del servicio que coloca (AutoPlacementJob).
/// </summary>
[Queue("bizcenter")]
public class ApplyDualLegDeltasJob
{
    private readonly AppDbContext _db;
    private readonly ILogger<ApplyDualLegDeltasJob> _logger;

    public ApplyDualLegDeltasJob(AppDbContext db, ILogger<ApplyDualLegDeltasJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    public async Task<int> ExecuteAsync(CancellationToken ct = default)
    {
        var n = await LegPointMaintenance.DrainAsync(_db, ct: ct);
        if (n > 0) _logger.LogInformation("ApplyDualLegDeltas: {Count} deltas de pierna aplicados.", n);
        return n;
    }
}
