using Hangfire;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.BizCenter.Jobs;

/// <summary>
/// Club Recognition: cada noche evalúa el día que acaba de cerrar (ayer en hora del servidor, IDateTimeProvider.Now). La
/// primera vez, sin el marcador ClubRecognition.HistoryLoadedThrough, hace la carga histórica hasta ese día (Reached = día
/// en que se cruzó el umbral); a partir de ahí, la detección nocturna (Reached = el día que cerró). Se programa a las
/// 05:45 UTC (00:45 en invierno, 01:45 en verano), 15 minutos después de la foto de países para no recorrer dos tablas
/// grandes a la vez. Relanzar: panel de Hangfire del BizCenter (/hangfire) -> Recurring jobs -> club-recognition ->
/// Trigger now. Es idempotente.
/// </summary>
[Queue("bizcenter")]
public class ClubRecognitionJob
{
    private readonly IClubRecognitionDetector    _detector;
    private readonly IDateTimeProvider           _clock;
    private readonly ILogger<ClubRecognitionJob> _logger;

    public ClubRecognitionJob(IClubRecognitionDetector detector, IDateTimeProvider clock, ILogger<ClubRecognitionJob> logger)
    {
        _detector = detector;
        _clock    = clock;
        _logger   = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var day = _clock.Now.Date.AddDays(-1);

        if (!await _detector.HistoryLoadedAsync(ct))
        {
            var loaded = await _detector.LoadHistoryAsync(day, ct);
            _logger.LogInformation("ClubRecognitionJob: carga historica hasta {Day:yyyy-MM-dd}, {Rows} registros nuevos.", day, loaded);
            return;
        }

        var added = await _detector.DetectDayAsync(day, ct);
        _logger.LogInformation("ClubRecognitionJob: {Rows} registros nuevos para {Day:yyyy-MM-dd}.", added, day);
    }
}
