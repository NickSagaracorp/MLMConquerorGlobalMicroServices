using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Reports;

/// <summary>
/// Deja hechos en segundo plano los reportes que arman la lista entera en memoria: Earnings 180 Days y Earnings Lifetime
/// (con el corte por defecto, ayer) y Lifetime Rank y Current Rank. Con un millón de miembros cada uno agrega millones
/// de filas (2,4M comisiones, 216k cambios de rango) y tardaba de 12 a 18 s la primera vez que alguien lo abría; así la
/// pantalla lee la lista ya calculada.
/// </summary>
/// <remarks>
/// <para>La caché de estos reportes es de memoria del proceso, así que cada réplica calcula la suya (no hay turno).</para>
/// <para>Calcula al arrancar (tras <see cref="Espera"/>, para no coincidir con el precálculo de paneles) y luego cada
/// <see cref="Intervalo"/>, pero solo mientras alguien use los reportes: si nadie los abrió en <see cref="SinUso"/>, la
/// vuelta se salta y la base descansa. Lo guardado dura más que el intervalo, así que entre vueltas no vence.</para>
/// <para>Otros cortes (una fecha elegida a mano) siguen calculándose al pedirlos, como antes.</para>
/// </remarks>
public sealed class PrecalculoDeReportes : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan SinUso    = TimeSpan.FromHours(2);
    public static readonly TimeSpan Espera    = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PrecalculoDeReportes> _logger;

    public PrecalculoDeReportes(IServiceScopeFactory scopes, ILogger<PrecalculoDeReportes> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(Espera, stoppingToken);
            await RecalcularAsync(primeraVuelta: true, stoppingToken);

            using var reloj = new PeriodicTimer(Intervalo);
            while (await reloj.WaitForNextTickAsync(stoppingToken))
                await RecalcularAsync(primeraVuelta: false, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado normal del servicio.
        }
    }

    /// <summary>Si hay que recalcular: la primera vuelta siempre; después, solo si el reporte se usó hace poco.</summary>
    public static bool EnUso(TimeSpan? tiempoSinUso, bool primeraVuelta) =>
        primeraVuelta || (tiempoSinUso is { } t && t <= SinUso);

    /// <summary>Una vuelta: recalcula cada reporte en uso; si uno falla, los demás siguen.</summary>
    public async Task RecalcularAsync(bool primeraVuelta, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var corte = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().Now.Date.AddDays(-1);

        if (EnUso(EarningsRankingService.TiempoSinUso, primeraVuelta))
        {
            var earnings = scope.ServiceProvider.GetRequiredService<IEarningsRankingService>();
            await ReporteAsync("Earnings 180 Days", () => earnings.PrecalcularAsync(corte, EarningsRankingService.DefaultWindow, ct));
            await ReporteAsync("Earnings Lifetime", () => earnings.PrecalcularAsync(corte, null, ct));
        }

        if (EnUso(RankReportService.TiempoSinUso, primeraVuelta))
        {
            var ranks = scope.ServiceProvider.GetRequiredService<IRankReportService>();
            await ReporteAsync("Lifetime Rank", () => ranks.PrecalcularAsync(RankReportMode.Lifetime, ct));
            await ReporteAsync("Current Rank", () => ranks.PrecalcularAsync(RankReportMode.Current, ct));
        }
    }

    private async Task ReporteAsync(string reporte, Func<Task> recalcular)
    {
        var inicio = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            await recalcular();
            _logger.LogInformation("Precálculo de reportes: {Reporte} en {Ms} ms.",
                reporte, (long)System.Diagnostics.Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Precálculo de reportes: falló {Reporte}; la pantalla lo calculará al abrirse.", reporte);
        }
    }
}
