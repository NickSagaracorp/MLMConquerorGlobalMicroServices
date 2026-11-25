using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using Microsoft.Extensions.Primitives;
using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

public interface IEarningsRankingService
{
    Task<PagedResult<EarningsRankRowView>> GetPageAsync(DateTime cutoff, int? windowDays, GridDataRequest request,
        CancellationToken ct = default);

    Task<IReadOnlyList<EarningsRankRowView>> GetAllAsync(DateTime cutoff, int? windowDays, string? search,
        CancellationToken ct = default);

    /// <summary>Fecha de la última foto semanal (el corte por defecto de la pantalla), o null si todavía no hay.</summary>
    Task<DateTime?> UltimaFotoAsync(CancellationToken ct = default);
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
        var (page, pageSize) = GridQueryableExtensions.ResolvePage(request);
        if (await FotoAsync(cutoff, windowDays, ct) is { } foto)
        {
            var filtradas = Buscar(foto, request.Search);
            return new PagedResult<EarningsRankRowView>
            {
                Items      = await AVista(filtradas.OrderBy(s => s.Rank).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct),
                TotalCount = await filtradas.CountAsync(ct),
                Page       = page,
                PageSize   = pageSize
            };
        }

        var rows = Filter(await RankingAsync(cutoff, windowDays, ct), request.Search);
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
        await FotoAsync(cutoff, windowDays, ct) is { } foto
            ? await AVista(Buscar(foto, search).OrderBy(s => s.Rank)).ToListAsync(ct)
            : Filter(await RankingAsync(cutoff, windowDays, ct), search);

    public async Task<DateTime?> UltimaFotoAsync(CancellationToken ct = default) =>
        await _db.EarningsRankingSnapshots.AsNoTracking().MaxAsync(s => (DateTime?)s.SnapshotDate, ct);

    /// <summary>
    /// Las filas de la foto semanal de (corte, ventana), o null si ese corte no tiene foto: solo se guardan la ventana de
    /// la foto (180 días) y de por vida, con fecha de domingo. Sin foto, el ranking se calcula al momento como antes.
    /// </summary>
    private async Task<IQueryable<EarningsRankingSnapshot>?> FotoAsync(DateTime cutoff, int? windowDays, CancellationToken ct)
    {
        var window = NormalizeWindow(windowDays);
        if (window is not null && window != ReportSnapshotWriter.VentanaDeLaFoto) return null;

        var dia   = cutoff.Date;
        var clave = window ?? EarningsRankingSnapshot.DePorVida;
        var filas = _db.EarningsRankingSnapshots.AsNoTracking().Where(s => s.SnapshotDate == dia && s.WindowDays == clave);
        return await filas.AnyAsync(ct) ? filas : null;
    }

    /// <summary>Nombre o correo que contienen el término, sin distinguir mayúsculas (como el filtro en memoria).</summary>
    private IQueryable<EarningsRankingSnapshot> Buscar(IQueryable<EarningsRankingSnapshot> filas, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return filas;
        var t = search.Trim().ToUpperInvariant();
        const string bin = GridQueryableExtensions.IntercalacionBinaria;
        // En SQL Server, en mayúsculas con intercalación binaria: recorre la foto, pero varias veces más barato.
        return _db.Database.IsRelational()
            ? filas.Where(s => EF.Functions.Collate(s.MemberName.ToUpper(), bin).Contains(t)
                            || EF.Functions.Collate(s.Email.ToUpper(), bin).Contains(t))
            : filas.Where(s => s.MemberName.ToUpper().Contains(t) || s.Email.ToUpper().Contains(t));
    }

    private static IQueryable<EarningsRankRowView> AVista(IQueryable<EarningsRankingSnapshot> filas) =>
        filas.Select(s => new EarningsRankRowView
        {
            Rank = s.Rank, MemberId = s.MemberId, Username = s.Username, MemberName = s.MemberName,
            Email = s.Email, Country = s.Country, Total = s.Total
        });

    private static List<EarningsRankRowView> Filter(IReadOnlyList<EarningsRankRowView> rows, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return rows.ToList();   // copia: nunca la lista cacheada
        var term = search.Trim();
        return rows.Where(r => r.MemberName.Contains(term, StringComparison.OrdinalIgnoreCase)
                            || r.Email.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private async Task<IReadOnlyList<EarningsRankRowView>> RankingAsync(DateTime cutoff, int? windowDays, CancellationToken ct)
    {
        var window = NormalizeWindow(windowDays);
        var day    = cutoff.Date;
        var today  = _clock.Now.Date;
        if (day > today) day = today;
        var key = $"reports:earnings:{day:yyyyMMdd}:{window?.ToString() ?? "all"}";

        if (TryGetCached(key, out var cached)) return cached;

        var gate = Locks[(uint)key.GetHashCode() % LockStripes];
        await gate.WaitAsync(ct);
        try
        {
            if (TryGetCached(key, out cached)) return cached;
            // El token se toma ANTES de leer: una invalidacion durante el calculo deja la entrada ya vencida.
            var token  = CurrentToken();
            var ranked = await ComputeAsync(day, window, ct);
            _cache.Set(key, ranked, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheFor }
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

    private Task<IReadOnlyList<EarningsRankRowView>> ComputeAsync(DateTime day, int? window, CancellationToken ct) =>
        CalcularAsync(_db, day, window, ct);

    /// <summary>El ranking completo de (corte, ventana), en orden. Lo usan la pantalla (sin foto) y la foto semanal.</summary>
    internal static async Task<IReadOnlyList<EarningsRankRowView>> CalcularAsync(AppDbContext db, DateTime day, int? window,
        CancellationToken ct)
    {
        var to   = day.AddDays(1);                                          // el corte incluye su día entero
        var from = window is null ? (DateTime?)null : day.AddDays(1 - window.Value);

        var totals = await (
            from t in EarningsQueries.TotalsByMember(db, @from, to)
            join m in db.MemberProfiles.AsNoTracking() on t.MemberId equals m.MemberId
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
