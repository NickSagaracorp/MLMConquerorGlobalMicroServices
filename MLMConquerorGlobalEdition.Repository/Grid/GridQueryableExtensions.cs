using System.Globalization;
using System.Linq.Dynamic.Core;
using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace MLMConquerorGlobalEdition.Repository.Grid;

/// <summary>
/// Applies a <see cref="GridDataRequest"/> (search · filter · sort · page) to any
/// <see cref="IQueryable{T}"/> entirely server-side, so a grid bound to one of these
/// queries searches and filters across the WHOLE dataset, not just the loaded page.
///
/// Filter/sort field names are validated against T's public properties before being
/// fed to the dynamic-LINQ parser, and all comparison values are passed as bound
/// parameters (@0, @1, …) — there is no string interpolation of user values, so the
/// dynamic predicate cannot be used for injection.
/// </summary>
public static class GridQueryableExtensions
{
    private const int DefaultMaxPageSize = 200;

    public static async Task<SharedKernel.PagedResult<T>> ToGridResultAsync<T>(
        this IQueryable<T> source,
        GridDataRequest request,
        string[] searchableFields,
        int maxPageSize = DefaultMaxPageSize,
        CancellationToken ct = default)
    {
        source = source.ApplySearch(request.Search, searchableFields);
        source = source.ApplyFilters(request.Filters);

        // Count AFTER filtering but BEFORE paging — this is the total matching set
        // the grid pager reports, so paging reflects the filtered universe.
        var total = await source.CountAsync(ct);

        source = source.ApplySorts(request.Sorts);

        var pageSize = Math.Clamp(request.PageSize <= 0 ? 20 : request.PageSize, 1, maxPageSize);
        var page     = Math.Max(1, request.Page);

        var items = await source
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new SharedKernel.PagedResult<T>
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        };
    }

    /// <summary>
    /// In-memory variant for already-materialized collections (e.g. a member's
    /// bounded team subtree, whose Leg/Level/percent columns are computed in C#
    /// and so cannot be projected into a DB query). Applies the same
    /// search → filter → sort → page pipeline so filtering/searching still span
    /// the WHOLE collection, not just the visible page.
    /// </summary>
    public static SharedKernel.PagedResult<T> ToGridResult<T>(
        this IEnumerable<T> source,
        GridDataRequest request,
        string[] searchableFields,
        int maxPageSize = DefaultMaxPageSize)
    {
        var q = source.AsQueryable();
        q = q.ApplySearch(request.Search, searchableFields);
        q = q.ApplyFilters(request.Filters);

        var total = q.Count();

        q = q.ApplySorts(request.Sorts);

        var pageSize = Math.Clamp(request.PageSize <= 0 ? 20 : request.PageSize, 1, maxPageSize);
        var page     = Math.Max(1, request.Page);

        var items = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new SharedKernel.PagedResult<T>
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        };
    }

    public static IQueryable<T> ApplySearch<T>(this IQueryable<T> source, string? search, string[] searchableFields)
    {
        if (string.IsNullOrWhiteSpace(search) || searchableFields is null || searchableFields.Length == 0)
            return source;

        // Only string columns are searchable with "contains".
        var fields = searchableFields
            .Select(GetProperty<T>)
            .Where(p => p is not null && p!.PropertyType == typeof(string))
            .Select(p => p!.Name)
            .ToArray();

        if (fields.Length == 0) return source;

        var term   = search.Trim().ToLower();
        var clause = string.Join(" || ",
            fields.Select(f => $"({f} != null && {f}.ToLower().Contains(@0))"));

        return source.Where(clause, term);
    }

    public static IQueryable<T> ApplyFilters<T>(this IQueryable<T> source, List<GridFilter>? filters)
    {
        if (filters is null || filters.Count == 0) return source;

        var predicate = new StringBuilder();
        var values    = new List<object?>();

        foreach (var f in filters)
        {
            var prop = GetProperty<T>(f.Field);
            if (prop is null) continue;

            var clause = BuildClause(prop, f, values);
            if (clause is null) continue;

            if (predicate.Length > 0)
                predicate.Append(string.Equals(f.Logic, "or", StringComparison.OrdinalIgnoreCase) ? " || " : " && ");

            predicate.Append('(').Append(clause).Append(')');
        }

        return predicate.Length == 0 ? source : source.Where(predicate.ToString(), values.ToArray());
    }

    public static IQueryable<T> ApplySorts<T>(this IQueryable<T> source, List<GridSort>? sorts)
    {
        if (sorts is null || sorts.Count == 0) return source;

        var ordering = string.Join(", ", sorts
            .Select(s => new { Prop = GetProperty<T>(s.Field), s.Direction })
            .Where(x => x.Prop is not null)
            .Select(x => $"{x.Prop!.Name} {(string.Equals(x.Direction, "desc", StringComparison.OrdinalIgnoreCase) ? "descending" : "ascending")}"));

        return string.IsNullOrEmpty(ordering) ? source : source.OrderBy(ordering);
    }

    private static string? BuildClause(PropertyInfo prop, GridFilter f, List<object?> values)
    {
        var name       = prop.Name;
        var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        var op         = (f.Operator ?? "contains").ToLowerInvariant();

        if (underlying == typeof(string))
        {
            var raw = f.Value;
            if (raw is null) return null;
            var v = raw.ToLower();
            var k = values.Count;
            values.Add(v);

            return op switch
            {
                "startswith" => $"{name} != null && {name}.ToLower().StartsWith(@{k})",
                "endswith"   => $"{name} != null && {name}.ToLower().EndsWith(@{k})",
                "equal"      => $"{name} != null && {name}.ToLower() == @{k}",
                "notequal"   => $"({name} == null || {name}.ToLower() != @{k})",
                _            => $"{name} != null && {name}.ToLower().Contains(@{k})",
            };
        }

        var typed = ConvertValue(f.Value, underlying);
        if (typed is null) return null;
        var idx = values.Count;
        values.Add(typed);

        return op switch
        {
            "notequal"           => $"{name} != @{idx}",
            "greaterthan"        => $"{name} > @{idx}",
            "greaterthanorequal" => $"{name} >= @{idx}",
            "lessthan"           => $"{name} < @{idx}",
            "lessthanorequal"    => $"{name} <= @{idx}",
            _                    => $"{name} == @{idx}",
        };
    }

    private static object? ConvertValue(string? raw, Type targetType)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            if (targetType.IsEnum)            return Enum.Parse(targetType, raw, ignoreCase: true);
            if (targetType == typeof(DateTime))
                return DateTime.Parse(raw, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            if (targetType == typeof(bool))   return bool.Parse(raw);
            if (targetType == typeof(Guid))   return Guid.Parse(raw);
            return Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
        }
        catch
        {
            // Unparseable filter value — drop the clause rather than 500 the request.
            return null;
        }
    }

    private static PropertyInfo? GetProperty<T>(string? field)
        => string.IsNullOrWhiteSpace(field)
            ? null
            : typeof(T).GetProperty(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
}
