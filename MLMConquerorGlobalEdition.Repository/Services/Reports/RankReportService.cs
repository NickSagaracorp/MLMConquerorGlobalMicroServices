using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

public interface IRankReportService
{
    /// <summary>Una página. La búsqueda llega en <see cref="GridDataRequest.Search"/> (la del filtro se ignora).</summary>
    Task<PagedResult<RankReportRowView>> GetPageAsync(RankReportFilter filter, GridDataRequest request,
        CancellationToken ct = default);

    /// <summary>Todas las filas que cumplen el filtro (exportación).</summary>
    Task<IReadOnlyList<RankReportRowView>> GetAllAsync(RankReportFilter filter, CancellationToken ct = default);
}

/// <summary>
/// Lifetime Rank y Current Rank: un Ambassador por fila con su rango (de por vida o vigente) y desde cuándo lo tiene.
/// Fuera: el rango 0, quien no tiene historial, Terminated, las cuentas corporativas y los clientes. La lista completa
/// de cada modo sale de UNA consulta SQL (<see cref="SourceQuery"/>) y se guarda 10 minutos en memoria; buscar, filtrar
/// y paginar trabajan sobre esa lista. Un cambio del indicador de cuenta corporativa la invalida con
/// <see cref="InvalidateAll"/>; un rango nuevo o un cambio de estado se ven, como mucho, 10 minutos después.
/// </summary>
public class RankReportService : IRankReportService
{
    /// <summary>Los estados que pueden aparecer (todos menos Terminated); también el valor por defecto del filtro.</summary>
    public static readonly MemberAccountStatus[] VisibleStatuses =
    {
        MemberAccountStatus.Active, MemberAccountStatus.Inactive, MemberAccountStatus.Suspended, MemberAccountStatus.Pending
    };

    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

    // Solo hay dos claves (una por modo): un cerrojo basta para que un pedido en frio calcule y el resto espere.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    // Cada entrada lleva el token vigente al calcular; InvalidateAll lo cancela y las vence todas.
    private static CancellationTokenSource _invalidation = new();

    /// <summary>Descarta las listas guardadas (p. ej. al cambiar el indicador de cuenta corporativa).</summary>
    public static void InvalidateAll() => Interlocked.Exchange(ref _invalidation, new CancellationTokenSource()).Cancel();

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public RankReportService(AppDbContext db, IMemoryCache cache)
    {
        _db    = db;
        _cache = cache;
    }

    /// <summary>Sin estados = los visibles; Terminated se descarta siempre.</summary>
    public static IReadOnlyCollection<MemberAccountStatus> NormalizeStatuses(IEnumerable<MemberAccountStatus>? statuses)
    {
        var set = (statuses ?? Array.Empty<MemberAccountStatus>())
            .Where(s => s != MemberAccountStatus.Terminated).Distinct().ToArray();
        return set.Length == 0 ? VisibleStatuses : set;
    }

    /// <summary>La consulta SQL del modo, con las exclusiones (expuesta para comprobar su traducción y medirla).</summary>
    public static IQueryable<RankReportSourceRow> SourceQuery(AppDbContext db, RankReportMode mode)
    {
        var ranks = mode == RankReportMode.Lifetime ? db.LifetimeRankSinceRows() : db.CurrentRankSinceRows();
        return from r in ranks
               where r.SortOrder > 0
               join d in db.RankDefinitions.AsNoTracking() on r.RankDefinitionId equals d.Id
               join m in db.MemberProfiles.AsNoTracking() on r.MemberId equals m.MemberId
               where m.MemberType == MemberType.Ambassador
                  && m.Status != MemberAccountStatus.Terminated
                  && !m.IsCorporateAccount
               select new RankReportSourceRow
               {
                   MemberId         = r.MemberId,
                   RankDefinitionId = r.RankDefinitionId,
                   RankName         = d.Name,
                   SortOrder        = r.SortOrder,
                   Since            = r.Since,
                   FirstName        = m.FirstName,
                   LastName         = m.LastName,
                   Email            = m.Email,
                   Country          = m.Country,
                   Status           = m.Status
               };
    }

    public async Task<PagedResult<RankReportRowView>> GetPageAsync(RankReportFilter filter, GridDataRequest request,
        CancellationToken ct = default)
    {
        var rows = Apply(await ListAsync(filter.Mode, ct), filter with { Search = request.Search });
        var (page, pageSize) = GridQueryableExtensions.ResolvePage(request);
        return new PagedResult<RankReportRowView>
        {
            Items      = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            TotalCount = rows.Count,
            Page       = page,
            PageSize   = pageSize
        };
    }

    public async Task<IReadOnlyList<RankReportRowView>> GetAllAsync(RankReportFilter filter, CancellationToken ct = default) =>
        Apply(await ListAsync(filter.Mode, ct), filter);

    /// <summary>Filtra la lista guardada. Devuelve siempre una lista nueva: nunca la cacheada.</summary>
    private static List<RankReportRowView> Apply(IReadOnlyList<RankReportRowView> rows, RankReportFilter f)
    {
        var statuses = NormalizeStatuses(f.Statuses);
        var from     = f.SinceFrom?.Date;
        var to       = f.SinceTo?.Date.AddDays(1);                         // hasta incluye su día entero
        var term     = string.IsNullOrWhiteSpace(f.Search) ? null : f.Search.Trim();

        return rows.Where(r =>
                statuses.Contains(r.Status)
                && (f.RankDefinitionId is null || r.RankDefinitionId == f.RankDefinitionId)
                && (from is null || r.Since >= from)
                && (to is null || r.Since < to)
                && (term is null
                    || r.MemberName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.Email.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.Country.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private async Task<IReadOnlyList<RankReportRowView>> ListAsync(RankReportMode mode, CancellationToken ct)
    {
        var key = $"reports:ranks:{mode}";
        if (_cache.TryGetValue(key, out IReadOnlyList<RankReportRowView>? cached) && cached is not null) return cached;

        await Gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(key, out cached) && cached is not null) return cached;
            // El token se toma ANTES de leer: una invalidacion durante el calculo deja la entrada ya vencida.
            var token  = Volatile.Read(ref _invalidation).Token;
            var source = await SourceQuery(_db, mode).ToListAsync(ct);
            IReadOnlyList<RankReportRowView> rows = source
                .OrderByDescending(r => r.SortOrder)
                .ThenByDescending(r => r.Since)
                .ThenBy(r => r.MemberId, StringComparer.Ordinal)
                .Select(r => new RankReportRowView
                {
                    MemberId         = r.MemberId,
                    MemberName       = $"{r.FirstName} {r.LastName}".Trim(),
                    Email            = r.Email,
                    Country          = r.Country,
                    Status           = r.Status,
                    RankDefinitionId = r.RankDefinitionId,
                    RankName         = r.RankName,
                    RankSortOrder    = r.SortOrder,
                    Since            = r.Since
                }).ToList().AsReadOnly();

            _cache.Set(key, rows, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheFor }
                .AddExpirationToken(new CancellationChangeToken(token)));
            return rows;
        }
        finally
        {
            Gate.Release();
        }
    }
}
