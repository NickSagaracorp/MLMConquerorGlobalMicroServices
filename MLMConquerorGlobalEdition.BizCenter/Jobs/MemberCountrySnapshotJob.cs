using Hangfire;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.BizCenter.Jobs;

/// <summary>
/// Foto diaria de miembros por pais (reporte Top Countries del admin). Cada noche escribe el dia que acaba de cerrar
/// (ayer en hora del servidor, IDateTimeProvider.Now) con el estado de ese momento; repetirlo reemplaza las filas del dia.
/// Se programa a las 05:30 UTC: con el servidor en UTC-4 (verano) son las 01:30 y en UTC-5 (invierno) las 00:30, siempre
/// despues de la medianoche local. El dia no sale de la hora del cron sino del reloj del servidor. Para relanzarlo:
/// panel de Hangfire del BizCenter (/hangfire) -> Recurring jobs -> member-country-snapshot -> Trigger now.
/// </summary>
[Queue("bizcenter")]
public class MemberCountrySnapshotJob
{
    private readonly IMemberCountrySnapshotWriter      _writer;
    private readonly IDateTimeProvider                 _clock;
    private readonly ILogger<MemberCountrySnapshotJob> _logger;

    public MemberCountrySnapshotJob(IMemberCountrySnapshotWriter writer, IDateTimeProvider clock,
        ILogger<MemberCountrySnapshotJob> logger)
    {
        _writer = writer;
        _clock  = clock;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var day  = _clock.Now.Date.AddDays(-1);
        var rows = await _writer.WriteAsync(day, ct);
        _logger.LogInformation("MemberCountrySnapshotJob: {Rows} filas para {Day:yyyy-MM-dd}.", rows, day);
    }
}
