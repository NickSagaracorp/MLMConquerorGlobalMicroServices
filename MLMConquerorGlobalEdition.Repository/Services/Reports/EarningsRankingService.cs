using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using Microsoft.Extensions.Primitives;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

public interface IEarningsRankingService
{
    Task<PagedResult<EarningsRankRowView>> GetPageAsync(DateTime cutoff, int? windowDays, GridDataRequest request,
        CancellationToken ct = default);

    Task<IReadOnlyList<EarningsRankRowView>> GetAllAsync(DateTime cutoff, int? windowDays, string? search,
        CancellationToken ct = default);

    /// <summary>
    /// Recalcula el ranking de (corte, ventana) aunque esté en caché y lo deja guardado <see cref="EarningsRankingService.PrecalculadoDura"/>.
    /// Lo usa el precálculo en segundo plano para que la pantalla no espere el cálculo completo.
    /// </summary>
    Task PrecalcularAsync(DateTime cutoff, int? windowDays, CancellationToken ct = default);
}

/// <summary>
/// Ranking de comisiones por miembro hasta una fecha de corte (Earnings Lifetime) o en una ventana de días (Earnings
/// 180 Days). Suma lo pagado y lo pendiente, sin canceladas ni cuentas corporativas. El ranking completo se calcula una
/// vez por (corte, ventana) y se guarda 10 minutos en memoria: buscar y paginar no lo recalculan y el Rank es siempre
/// la posición entre todos.
/// <para>Los resultados pueden estar desfasados hasta 10 minutos: una comisión nueva o el cambio del indicador de cuenta
/// corporativa invalida todo con <see cref="InvalidateAll"/>.</para>
/// <para>El corte se acota a la fecha de hoy (hora de servidor): un corte futuro se trata como hoy.</para>
/// <para>Los pedidos simultáneos en frío para la misma clave se serializan: se calcula una sola vez y el resto lee la
/// entrada ya guardada.</para>
/// </summary>
public class EarningsRankingService : IEarningsRankingService
{
    public static readonly int[] AllowedWindows = { 30, 90, 180, 365 };
    public const int DefaultWindow = 180;
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Lo que dura un ranking que dejó el precálculo: más que su intervalo, para que entre dos vueltas la pantalla
    /// siempre lo encuentre hecho. El precálculo lo reescribe antes de que venza.
    /// </summary>
    public static readonly TimeSpan PrecalculadoDura = TimeSpan.FromMinutes(25);

    private static long _ultimoUso;

    /// <summary>
    /// Tiempo desde la última página o exportación pedida (reloj monótono del proceso: solo mide lo transcurrido);
    /// null si nunca se pidió. El precálculo deja de recalcular cuando nadie usa el reporte.
    /// </summary>
    public static TimeSpan? TiempoSinUso
    {
        get { var t = Interlocked.Read(ref _ultimoUso); return t == 0 ? null : TimeSpan.FromMilliseconds(Environment.TickCount64 - t); }
    }

    // Cerrojos por tramos: un arreglo fijo evita que el diccionario de cerrojos crezca con cada clave (corte x ventana).
    private const int LockStripes = 32;
    private static readonly SemaphoreSlim[] Locks = Enumerable.Range(0, LockStripes).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    // Invalida todas las entradas guardadas: cada una lleva el token de la fuente vigente al calcular.
    private static CancellationTokenSource _invalidation = new();

    // Si InvalidateAll cancelo y desecho la fuente justo despues de leerla, la entrada nace vencida (token ya cancelado).
    private static CancellationToken CurrentToken()
    {
        try { return Volatile.Read(ref _invalidation).Token; }
        catch (ObjectDisposedException) { return new CancellationToken(true); }
    }

    /// <summary>Descarta todo ranking guardado (p. ej. al cambiar el indicador de cuenta corporativa).</summary>
    public static void InvalidateAll()
    {
        var old = Interlocked.Exchange(ref _invalidation, new CancellationTokenSource());
        old.Cancel();
        old.Dispose();
    }

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IDateTimeProvider _clock;

    public EarningsRankingService(AppDbContext db, IMemoryCache cache, IDateTimeProvider clock)
    {
        _db    = db;
        _cache = cache;
        _clock = clock;
    }

    public static int? NormalizeWindow(int? days) =>
        days is null ? null : AllowedWindows.Contains(days.Value) ? days : DefaultWindow;

    public async Task<PagedResult<EarningsRankRowView>> GetPageAsync(DateTime cutoff, int? windowDays,
        GridDataRequest request, CancellationToken ct = default)
    {
        var rows = Filter(await RankingAsync(cutoff, windowDays, ct), request.Search);
        var (page, pageSize) = GridQueryableExtensions.ResolvePage(request);
        return new PagedResult<EarningsRankRowView>
        {
            Items      = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            TotalCount = rows.Count,
            Page       = page,
            PageSize   = pageSize
        };
    }

    public async Task<IReadOnlyList<EarningsRankRowView>> GetAllAsync(DateTime cutoff, int? windowDays, string? search,
        CancellationToken ct = default) =>
        Filter(await RankingAsync(cutoff, windowDays, ct), search);

    private static List<EarningsRankRowView> Filter(IReadOnlyList<EarningsRankRowView> rows, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return rows.ToList();   // copia: nunca la lista cacheada
        var term = search.Trim();
        return rows.Where(r => r.MemberName.Contains(term, StringComparison.OrdinalIgnoreCase)
                            || r.Email.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task PrecalcularAsync(DateTime cutoff, int? windowDays, CancellationToken ct = default) =>
        await RankingAsync(cutoff, windowDays, ct, recalcular: true);

    private async Task<IReadOnlyList<EarningsRankRowView>> RankingAsync(DateTime cutoff, int? windowDays,
        CancellationToken ct, bool recalcular = false)
    {
        if (!recalcular) Interlocked.Exchange(ref _ultimoUso, Environment.TickCount64);
        var window = NormalizeWindow(windowDays);
        var day    = cutoff.Date;
        var today  = _clock.Now.Date;
        if (day > today) day = today;
        var key = $"reports:earnings:{day:yyyyMMdd}:{window?.ToString() ?? "all"}";

        if (!recalcular && TryGetCached(key, out var cached)) return cached;

        var gate = Locks[(uint)key.GetHashCode() % LockStripes];
        await gate.WaitAsync(ct);
        try
        {
            if (!recalcular && TryGetCached(key, out cached)) return cached;
            // El token se toma ANTES de leer: una invalidacion durante el calculo deja la entrada ya vencida.
            var token  = CurrentToken();
            var ranked = await ComputeAsync(day, window, ct);
            _cache.Set(key, ranked, new MemoryCacheEntryOptions
                    { AbsoluteExpirationRelativeToNow = recalcular ? PrecalculadoDura : CacheFor }
                .AddExpirationToken(new CancellationChangeToken(token)));
            return ranked;
        }
        finally
        {
            gate.Release();
        }
    }

    private bool TryGetCached(string key, out IReadOnlyList<EarningsRankRowView> rows)
    {
        if (_cache.TryGetValue(key, out IReadOnlyList<EarningsRankRowView>? cached) && cached is not null)
        {
            rows = cached;
            return true;
        }
        rows = Array.Empty<EarningsRankRowView>();
        return false;
    }

    private async Task<IReadOnlyList<EarningsRankRowView>> ComputeAsync(DateTime day, int? window, CancellationToken ct)
    {
        var to   = day.AddDays(1);                                          // el corte incluye su día entero
        var from = window is null ? (DateTime?)null : day.AddDays(1 - window.Value);

        var totals = await (
            from t in EarningsQueries.TotalsByMember(_db, @from, to)
            join m in _db.MemberProfiles.AsNoTracking() on t.MemberId equals m.MemberId
            where !m.IsCorporateAccount && t.Total > 0
            select new
            {
                t.MemberId, t.Total, m.ReplicateSiteSlug, m.FirstName, m.LastName, m.Email, m.Country
            }).ToListAsync(ct);

        return totals
            .OrderByDescending(t => t.Total).ThenBy(t => t.MemberId, StringComparer.Ordinal)
            .Select((t, i) => new EarningsRankRowView
            {
                Rank       = i + 1,
                MemberId   = t.MemberId,
                Username   = t.ReplicateSiteSlug,
                MemberName = $"{t.FirstName} {t.LastName}".Trim(),
                Email      = t.Email,
                Country    = t.Country,
                Total      = t.Total
            }).ToList().AsReadOnly();
    }
}
