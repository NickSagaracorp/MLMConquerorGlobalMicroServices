using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetCeoDashboard;
using MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetFinancialDashboard;
using MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetGrowthDashboard;
using MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetHealthDashboard;
using MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMemberStats;
using MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMembers;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards;

/// <summary>
/// Recalcula en segundo plano, al arrancar y cada <see cref="CacheKeys.AdminPanelesIntervalo"/>, los paneles del
/// admin (CEO, financiero del mes en curso, crecimiento y salud), los contadores y la primera página del listado de
/// miembros, y los deja
/// en la caché donde los leen las pantallas. Con un millón de miembros el panel CEO hace unas 30 agregaciones que
/// suman cerca de un minuto: así nadie las espera al abrir la pantalla. "Refresh" sigue recalculando al momento.
/// </summary>
/// <remarks>
/// Con varias réplicas de AdminAPI solo recalcula la que gana el turno de la vuelta (SET NX en Redis): las demás
/// leen lo que dejó. Si no se puede pedir el turno, esa vuelta no se hace; la caché dura tres intervalos, así que
/// una vuelta perdida no deja la pantalla sin datos. Cada panel se recalcula aparte: si uno falla, los demás siguen.
/// </remarks>
public sealed class PrecalculoDePaneles : BackgroundService
{
    internal const string ClaveDelTurno = "admin:paneles:turno";

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PrecalculoDePaneles> _logger;

    public PrecalculoDePaneles(IServiceScopeFactory scopes, ILogger<PrecalculoDePaneles> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var reloj = new PeriodicTimer(CacheKeys.AdminPanelesIntervalo);
        try
        {
            do
            {
                await RecalcularAsync(stoppingToken);
            }
            while (await reloj.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado normal del servicio.
        }
    }

    /// <summary>Una vuelta: pide el turno y, si lo gana, recalcula cada panel con la caché saltada.</summary>
    /// <returns>Si esta réplica hizo la vuelta.</returns>
    public async Task<bool> RecalcularAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

        bool turno;
        try
        {
            // Un poco menos que el intervalo, para que la vuelta siguiente de cualquier réplica lo encuentre libre.
            turno = await cache.TryClaimOnceAsync(
                ClaveDelTurno, CacheKeys.AdminPanelesIntervalo - TimeSpan.FromSeconds(30), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Precálculo de paneles: no se pudo pedir el turno; se salta esta vuelta.");
            return false;
        }

        if (!turno) return false;

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var ahora    = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().Now;

        // El financiero se pide como lo pide la pantalla al abrir: del día 1 del mes a hoy, por fecha.
        var inicioDelMes = new DateTime(ahora.Year, ahora.Month, 1);

        await PanelAsync("CEO", () => mediator.Send(new GetCeoDashboardQuery(BypassCache: true), ct));
        await PanelAsync("contadores de miembros", () => mediator.Send(new GetMemberStatsQuery(BypassCache: true), ct));
        await PanelAsync("financiero", () => mediator.Send(
            new GetFinancialDashboardQuery(inicioDelMes, ahora.Date, BypassCache: true), ct));
        await PanelAsync("crecimiento", () => mediator.Send(new GetGrowthDashboardQuery(BypassCache: true), ct));
        await PanelAsync("salud", () => mediator.Send(new GetHealthDashboardQuery(BypassCache: true), ct));

        // La primera página del listado de miembros, como la pide la pantalla al abrir (50 por página, sin filtros).
        // Su consulta es barata con el índice, pero la primera vez que el proceso la arma EF tarda unos 3 s en
        // compilarla: hacerlo aquí al arrancar evita que ese primer admin tras un despliegue lo espere.
        await PanelAsync("listado de miembros", () => mediator.Send(
            new GetMembersQuery(new PagedRequest { Page = 1, PageSize = 50 }, StatusFilter: null, BypassCache: true), ct));
        return true;
    }

    private async Task PanelAsync(string panel, Func<Task> recalcular)
    {
        var inicio = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            await recalcular();
            _logger.LogInformation("Precálculo de paneles: {Panel} en {Ms} ms.",
                panel, (long)System.Diagnostics.Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Precálculo de paneles: falló {Panel}; la pantalla sigue con la copia anterior.", panel);
        }
    }
}
