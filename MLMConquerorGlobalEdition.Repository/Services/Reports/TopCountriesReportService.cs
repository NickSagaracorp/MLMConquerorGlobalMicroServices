using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

public interface ITopCountriesReportService
{
    /// <summary>Tabla de países de la última foto con fecha ≤ <paramref name="date"/>, con Change contra 30 días antes.</summary>
    Task<TopCountriesTableView> GetTableAsync(DateTime date, IReadOnlyCollection<MemberAccountStatus>? statuses,
        CancellationToken ct = default);

    /// <summary>Total de la última foto de cada período.</summary>
    Task<IReadOnlyList<CountryChartPointView>> GetChartAsync(IReadOnlyList<ReportPeriod> periods,
        IReadOnlyCollection<MemberAccountStatus>? statuses, CancellationToken ct = default);
}

/// <summary>
/// Top Countries: lee solo la foto diaria (MemberCountrySnapshots), unas filas por día, así que no necesita caché. Sin
/// estados = los cinco (Total cuenta a todos; el estado es un filtro).
/// </summary>
public class TopCountriesReportService : ITopCountriesReportService
{
    public const int CompareDays = 30;

    private static readonly MemberAccountStatus[] AllStatuses = Enum.GetValues<MemberAccountStatus>();

    private readonly AppDbContext _db;

    public TopCountriesReportService(AppDbContext db) => _db = db;

    public static MemberAccountStatus[] NormalizeStatuses(IEnumerable<MemberAccountStatus>? statuses)
    {
        var set = (statuses ?? Array.Empty<MemberAccountStatus>()).Distinct().ToArray();
        return set.Length == 0 ? AllStatuses : set;
    }

    public async Task<TopCountriesTableView> GetTableAsync(DateTime date, IReadOnlyCollection<MemberAccountStatus>? statuses,
        CancellationToken ct = default)
    {
        var st       = NormalizeStatuses(statuses);
        var snapshot = await LatestOnOrBeforeAsync(date.Date, ct);
        if (snapshot is null) return new TopCountriesTableView();
        var compared = await LatestOnOrBeforeAsync(snapshot.Value.AddDays(-CompareDays), ct);

        var current = await CountsAsync(snapshot.Value, st, ct);
        var before  = compared is null
            ? null
            : (await CountsAsync(compared.Value, st, ct))
                .GroupBy(c => c.Country, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(c => c.Count), StringComparer.OrdinalIgnoreCase);

        var rows = current
            .GroupBy(c => c.Country, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var ambassadors = g.Where(c => c.MemberType == MemberType.Ambassador).Sum(c => c.Count);
                var customers   = g.Where(c => c.MemberType == MemberType.ExternalMember).Sum(c => c.Count);
                var total       = ambassadors + customers;
                return new CountryRowView
                {
                    Country     = g.Key,
                    Total       = total,
                    Ambassadors = ambassadors,
                    Customers   = customers,
                    Change      = before is null ? null : (int?)(total - before.GetValueOrDefault(g.Key))
                };
            })
            .Where(r => r.Total > 0)
            .OrderByDescending(r => r.Total).ThenBy(r => r.Country, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new TopCountriesTableView { SnapshotDate = snapshot, ComparedWith = compared, Rows = rows };
    }

    public async Task<IReadOnlyList<CountryChartPointView>> GetChartAsync(IReadOnlyList<ReportPeriod> periods,
        IReadOnlyCollection<MemberAccountStatus>? statuses, CancellationToken ct = default)
    {
        if (periods.Count == 0) return Array.Empty<CountryChartPointView>();
        var st    = NormalizeStatuses(statuses);
        var first = periods.Min(p => p.Start);
        var last  = periods.Max(p => p.End);

        var inRange = _db.MemberCountrySnapshots.AsNoTracking()
            .Where(s => s.SnapshotDate >= first && s.SnapshotDate <= last);
        // Los dias con foto (con cualquier estado) y, aparte, los totales de los estados elegidos: una foto sin filas de
        // esos estados da 0, no «sin foto».
        var days   = await inRange.Select(s => s.SnapshotDate).Distinct().ToListAsync(ct);
        var totals = await inRange.Where(s => st.Contains(s.Status))
            .GroupBy(s => s.SnapshotDate)
            .Select(g => new { Day = g.Key, Total = g.Sum(s => s.MemberCount) })
            .ToDictionaryAsync(x => x.Day, x => x.Total, ct);

        return periods.Select(p =>
        {
            var day = days.Where(d => d >= p.Start && d <= p.End).Select(d => (DateTime?)d).Max();
            return new CountryChartPointView
            {
                Label        = p.Label,
                Start        = p.Start,
                End          = p.End,
                SnapshotDate = day,
                Total        = day is null ? null : (int?)totals.GetValueOrDefault(day.Value)
            };
        }).ToList();
    }

    private Task<DateTime?> LatestOnOrBeforeAsync(DateTime day, CancellationToken ct) =>
        _db.MemberCountrySnapshots.AsNoTracking()
            .Where(s => s.SnapshotDate <= day)
            .MaxAsync(s => (DateTime?)s.SnapshotDate, ct);

    private sealed record CountryCount(string Country, MemberType MemberType, int Count);

    private async Task<List<CountryCount>> CountsAsync(DateTime day, MemberAccountStatus[] statuses, CancellationToken ct) =>
        (await _db.MemberCountrySnapshots.AsNoTracking()
            .Where(s => s.SnapshotDate == day && statuses.Contains(s.Status))
            .GroupBy(s => new { s.Country, s.MemberType })
            .Select(g => new { g.Key.Country, g.Key.MemberType, Count = g.Sum(s => s.MemberCount) })
            .ToListAsync(ct))
        .Select(x => new CountryCount(x.Country, x.MemberType, x.Count))
        .ToList();
}
