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
/// Trigger now. Es idempotente. Si faltaron noches, se pone al dia desde el marcador (hasta 31 dias por corrida).
/// </summary>
[Queue("bizcenter")]
[DisableConcurrentExecution(timeoutInSeconds: 3600)]
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

    /// <summary>Tope de dias por corrida al ponerse al dia tras fallos o paradas.</summary>
    public const int MaxCatchUpDays = 31;

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var yesterday = _clock.Now.Date.AddDays(-1);

        var last = await _detector.LastProcessedDayAsync(ct);
        if (last is null)
        {
            var loaded = await _detector.LoadHistoryAsync(yesterday, ct);
            _logger.LogInformation("ClubRecognitionJob: carga historica hasta {Day:yyyy-MM-dd}, {Rows} registros nuevos.", yesterday, loaded);
            return;
        }

        // Desde el dia siguiente al ultimo procesado hasta ayer (el detector adelanta el marcador tras cada dia). Si el
        // marcador ya esta en ayer (o despues) se reevalua ayer, como antes: es idempotente.
        var first = last.Value.Date.AddDays(1);
        if (first > yesterday) first = yesterday;
        var days = Math.Min((yesterday - first).Days + 1, MaxCatchUpDays);

        for (var i = 0; i < days; i++)
        {
            var day   = first.AddDays(i);
            var added = await _detector.DetectDayAsync(day, ct);
            _logger.LogInformation("ClubRecognitionJob: {Rows} registros nuevos para {Day:yyyy-MM-dd}.", added, day);
        }
    }
}
