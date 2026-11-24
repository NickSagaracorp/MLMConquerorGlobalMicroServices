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

    /// <summary>
    /// Verdadero cuando la base ya compara texto sin distinguir mayúsculas (la intercalación de SQL Server del sistema
    /// es SQL_Latin1_General_CP1_CI_AS): entonces búsquedas y filtros comparan la columna tal cual, sin ToLower.
    /// Con LOWER(columna) SQL Server no puede usar ningún índice y con un millón de miembros cada filtro recorría la
    /// tabla entera (4–7 s por lectura); sin él, "igual a" y "empieza por" buscan en el índice. Lo encienden al
    /// arrancar las APIs que consultan SQL Server; apagado (pruebas en memoria, colecciones ya cargadas) se sigue
    /// pasando todo a minúsculas, que es lo que hace falta donde la comparación sí distingue.
    /// </summary>
    public static bool BaseSinDistincionDeMayusculas { get; set; }

    public static async Task<SharedKernel.PagedResult<T>> ToGridResultAsync<T>(
        this IQueryable<T> source,
        GridDataRequest request,
        string[] searchableFields,
        int maxPageSize = DefaultMaxPageSize,
        CancellationToken ct = default,
        int? unfilteredTotal = null)
    {
        var unfiltered = string.IsNullOrWhiteSpace(request.Search) && (request.Filters is null || request.Filters.Count == 0);
        source = source.ApplySearch(request.Search, searchableFields);
        source = source.ApplyFilters(request.Filters);

        // Count AFTER filtering but BEFORE paging — this is the total matching set
        // the grid pager reports, so paging reflects the filtered universe. Sin busqueda
        // ni filtros, quien llama puede dar el total que ya conoce (p. ej. los contadores de pierna del binario) y no
        // se cuenta un rango de un millon de filas.
        var total = unfiltered && unfilteredTotal is { } conocido ? conocido : await source.CountAsync(ct);

        source = source.ApplySorts(request.Sorts);

        var (page, pageSize) = ResolvePage(request, maxPageSize);

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

    /// <summary>
    /// La pagina (desde 1) y su tamano tal como los aplica <see cref="ToGridResultAsync{T}"/>: para quien pagina por su
    /// cuenta antes de proyectar (Mi equipo por estructura pagina el arbol y despues une los perfiles).
    /// </summary>
    public static (int Page, int PageSize) ResolvePage(GridDataRequest request, int maxPageSize = DefaultMaxPageSize) =>
        (Math.Max(1, request.Page), Math.Clamp(request.PageSize <= 0 ? 20 : request.PageSize, 1, maxPageSize));

    /// <param name="porPrefijo">
    /// Busca los valores que EMPIEZAN por el término en vez de los que lo contienen. "Contiene" (LIKE '%x%') no puede
    /// usar índice y con un millón de filas tarda segundos; "empieza por" busca en el índice de cada columna.
    /// </param>
    public static IQueryable<T> ApplySearch<T>(this IQueryable<T> source, string? search, string[] searchableFields,
        bool? baseSinMayusculas = null, bool porPrefijo = false)
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

        var sinMinusculas = (baseSinMayusculas ?? BaseSinDistincionDeMayusculas) && source.Provider is not EnumerableQuery;
        var term   = sinMinusculas ? search.Trim() : search.Trim().ToLower();
        var metodo = porPrefijo ? "StartsWith" : "Contains";
        var clause = string.Join(" || ",
            fields.Select(f => sinMinusculas
                ? $"({f} != null && {f}.{metodo}(@0))"
                : $"({f} != null && {f}.ToLower().{metodo}(@0))"));

        return source.Where(clause, term);
    }

    public static IQueryable<T> ApplyFilters<T>(this IQueryable<T> source, List<GridFilter>? filters,
        bool? baseSinMayusculas = null)
    {
        if (filters is null || filters.Count == 0) return source;

        var predicate = new StringBuilder();
        var values    = new List<object?>();

        foreach (var f in filters)
        {
            var prop = GetProperty<T>(f.Field);
            if (prop is null) continue;

            var clause = BuildClause(prop, f, values, (baseSinMayusculas ?? BaseSinDistincionDeMayusculas) && source.Provider is not EnumerableQuery);
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

    private static string? BuildClause(PropertyInfo prop, GridFilter f, List<object?> values, bool sinMinusculas)
    {
        var name       = prop.Name;
        var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        var op         = (f.Operator ?? "contains").ToLowerInvariant();

        if (underlying == typeof(string))
        {
            var raw = f.Value;
            if (raw is null) return null;
            var v = sinMinusculas ? raw : raw.ToLower();
            var k = values.Count;
            values.Add(v);

            // La base ya compara sin mayúsculas: la columna va tal cual para que el índice sirva.
            if (sinMinusculas)
            {
                return op switch
                {
                    "startswith" => $"{name} != null && {name}.StartsWith(@{k})",
                    "endswith"   => $"{name} != null && {name}.EndsWith(@{k})",
                    "equal"      => $"{name} == @{k}",
                    "notequal"   => $"({name} == null || {name} != @{k})",
                    _            => $"{name} != null && {name}.Contains(@{k})",
                };
            }

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
