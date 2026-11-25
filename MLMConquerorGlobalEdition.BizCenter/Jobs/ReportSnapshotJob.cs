using Hangfire;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.BizCenter.Jobs;

/// <summary>
/// Foto semanal de los reportes del admin Earnings 180 Days, Earnings Lifetime, Lifetime Rank y Current Rank, como el CSV
/// de los lunes del sistema anterior: cada lunes de madrugada escribe la semana que cerró (el domingo, en hora del
/// servidor) y la pantalla la lee sin recalcular. Al arrancar el BizCenter, si falta la foto de la última semana cerrada
/// (primer despliegue o un lunes perdido), se encola una vez con <see cref="ExecuteIfMissingAsync"/>.
/// Para relanzarlo: panel de Hangfire del BizCenter (/hangfire) -> Recurring jobs -> report-snapshot -> Trigger now.
/// </summary>
[Queue("bizcenter")]
public class ReportSnapshotJob
{
    private readonly IReportSnapshotWriter      _writer;
    private readonly IDateTimeProvider          _clock;
    private readonly ILogger<ReportSnapshotJob> _logger;

    public ReportSnapshotJob(IReportSnapshotWriter writer, IDateTimeProvider clock, ILogger<ReportSnapshotJob> logger)
    {
        _writer = writer;
        _clock  = clock;
        _logger = logger;
    }

    /// <summary>Escribe (o reescribe) la foto de la última semana cerrada.</summary>
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var domingo = ReportSnapshotWriter.UltimoDomingoCerrado(_clock.Now);
        var inicio  = System.Diagnostics.Stopwatch.GetTimestamp();
        var r       = await _writer.WriteAsync(domingo, ct);
        _logger.LogInformation(
            "ReportSnapshotJob: foto del {Domingo:yyyy-MM-dd} en {Ms} ms (Earnings 180: {E180}, Lifetime: {EVida}, Lifetime Rank: {RVida}, Current Rank: {RHoy}).",
            domingo, (long)System.Diagnostics.Stopwatch.GetElapsedTime(inicio).TotalMilliseconds,
            r.Earnings180, r.EarningsLifetime, r.LifetimeRank, r.CurrentRank);
    }

    /// <summary>La escribe solo si todavía no existe (arranque del servicio).</summary>
    public async Task ExecuteIfMissingAsync(CancellationToken ct)
    {
        var domingo = ReportSnapshotWriter.UltimoDomingoCerrado(_clock.Now);
        if (await _writer.ExistsAsync(domingo, ct)) return;
        await ExecuteAsync(ct);
    }
}
