using System.Globalization;
using System.Linq.Expressions;
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

    /// <summary>
    /// Cuánto se guarda el total de una lectura del grid (null = se cuenta siempre). El grid pide el total en CADA
    /// lectura, también al pasar de página, y un COUNT sobre 1–2,4 millones de filas tarda 0,5–3 s. Con esto se cuenta
    /// una vez por combinación de filtros: la clave es el SQL de la consulta ya filtrada, con los valores de sus
    /// parámetros, así que otro filtro, otra búsqueda u otro filtro de la página cuentan de nuevo. La página de filas
    /// nunca sale de aquí, siempre es fresca; solo el número del paginador puede ir unos segundos por detrás. Lo
    /// encienden al arrancar las APIs que consultan SQL Server; "Refresh" pide recontar.
    /// </summary>
    public static TimeSpan? DuracionDeTotalesEnCache { get; set; }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int Total, DateTime Vence)> _totales = new();
    private const int MaximoDeTotalesGuardados = 5_000;

    private static async Task<int> ContarAsync<T>(IQueryable<T> source, bool recontar, CancellationToken ct)
    {
        if (DuracionDeTotalesEnCache is not { } duracion || source.Provider is EnumerableQuery)
            return await source.CountAsync(ct);

        string clave;
        try
        {
            var sql = source.ToQueryString();
            if (string.IsNullOrWhiteSpace(sql)) return await source.CountAsync(ct);
            clave = typeof(T).FullName + "|" + sql;
        }
        catch (Exception)
        {
            // Un proveedor que no sabe escribir su SQL (pruebas en memoria): se cuenta siempre.
            return await source.CountAsync(ct);
        }

        var ahora = DateTime.UtcNow;
        if (!recontar && _totales.TryGetValue(clave, out var guardado) && guardado.Vence > ahora)
            return guardado.Total;

        var total = await source.CountAsync(ct);

        if (_totales.Count >= MaximoDeTotalesGuardados)
            foreach (var vencido in _totales.Where(t => t.Value.Vence <= ahora).Select(t => t.Key).ToList())
                _totales.TryRemove(vencido, out _);
        if (_totales.Count < MaximoDeTotalesGuardados)
            _totales[clave] = (total, ahora + duracion);

        return total;
    }

    public static async Task<SharedKernel.PagedResult<T>> ToGridResultAsync<T>(
        this IQueryable<T> source,
        GridDataRequest request,
        string[] searchableFields,
        int maxPageSize = DefaultMaxPageSize,
        CancellationToken ct = default,
        int? unfilteredTotal = null,
        bool recontar = false)
    {
        var unfiltered = string.IsNullOrWhiteSpace(request.Search) && (request.Filters is null || request.Filters.Count == 0);
        source = source.ApplySearch(request.Search, searchableFields);
        source = source.ApplyFilters(request.Filters);

        // Count AFTER filtering but BEFORE paging — this is the total matching set
        // the grid pager reports, so paging reflects the filtered universe. Sin busqueda
        // ni filtros, quien llama puede dar el total que ya conoce (p. ej. los contadores de pierna del binario) y no
        // se cuenta un rango de un millon de filas.
        var total = unfiltered && unfilteredTotal is { } conocido ? conocido : await ContarAsync(source, recontar, ct);

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

        var sinMinusculas = (baseSinMayusculas ?? BaseSinDistincionDeMayusculas) && source.Provider is not EnumerableQuery;

        // "Contiene" y "termina en" de texto no pueden usar índice: en SQL Server se comparan en mayúsculas con
        // intercalación binaria (ver FiltroBinario). El predicado se arma como árbol de expresiones para que esas
        // cláusulas convivan con las demás también cuando se unen con "or", con la misma precedencia que antes
        // ("and" pesa más que "or": a || b && c es a || (b && c)).
        var x      = Expression.Parameter(typeof(T), "x");
        var values = new List<object?>();
        // Cada grupo es una cadena de "and"; los grupos se unen con "or". Las cláusulas de texto se traducen al final,
        // cuando ya están todos los valores (@0, @1, …), por eso se guardan como funciones.
        var grupos = new List<List<Func<Expression>>>();
        object?[]? parametros = null;
        Expression Traducir(string texto)
        {
            parametros ??= values.ToArray();
            var lambda = DynamicExpressionParser.ParseLambda(typeof(T), typeof(bool), texto, parametros);
            return new CambioDeParametro(lambda.Parameters[0], x).Visit(lambda.Body);
        }

        foreach (var f in filters)
        {
            var prop = GetProperty<T>(f.Field);
            if (prop is null) continue;

            var filtro = sinMinusculas ? ConPrefijoSiEsCorto(prop, f) : f;
            Func<Expression> condicion;

            if (sinMinusculas && prop.PropertyType == typeof(string) && !string.IsNullOrEmpty(filtro.Value)
                && filtro.Operator?.ToLowerInvariant() is "contains" or "endswith" or null)
            {
                var binario = FiltroBinario(x, prop, (filtro.Operator ?? "contains").ToLowerInvariant(), filtro.Value!);
                condicion = () => binario;
            }
            else
            {
                var clause = BuildClause(prop, filtro, values, sinMinusculas);
                if (clause is null) continue;
                condicion = () => Traducir(clause);
            }

            if (grupos.Count == 0 || string.Equals(f.Logic, "or", StringComparison.OrdinalIgnoreCase))
                grupos.Add(new List<Func<Expression>>());
            grupos[^1].Add(condicion);
        }

        if (grupos.Count == 0) return source;

        var cuerpo = grupos
            .Select(g => g.Select(c => c()).Aggregate(Expression.AndAlso))
            .Aggregate(Expression.OrElse);

        return source.Where(Expression.Lambda<Func<T, bool>>(cuerpo, x));
    }

    private sealed class CambioDeParametro(ParameterExpression de, ParameterExpression a) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == de ? a : node;
    }

    /// <summary>Intercalación con la que se compara "contiene" en SQL Server: binaria, sobre el texto en mayúsculas.</summary>
    public const string IntercalacionBinaria = "Latin1_General_100_BIN2";

    /// <summary>
    /// Mínimo de caracteres para que "contiene" busque a mitad de texto. Con menos, una o dos letras coinciden con casi
    /// toda la tabla y el recorrido no aporta nada: se busca "empieza por", que usa el índice.
    /// </summary>
    public const int MinimoParaContiene = 3;

    private static GridFilter ConPrefijoSiEsCorto(PropertyInfo prop, GridFilter f)
    {
        var op = (f.Operator ?? "contains").ToLowerInvariant();
        if (prop.PropertyType != typeof(string) || op is not ("contains" or "endswith")) return f;
        if ((f.Value?.Trim().Length ?? 0) >= MinimoParaContiene) return f;
        return new GridFilter { Field = f.Field, Operator = "startswith", Value = f.Value?.Trim(), Logic = f.Logic };
    }

    /// <summary>
    /// <c>UPPER(columna) COLLATE Latin1_General_100_BIN2 LIKE '%VALOR%'</c> (o '%VALOR' para "termina en"). Sigue
    /// recorriendo la columna —ningún índice sirve para buscar a mitad de texto—, pero la comparación binaria es mucho
    /// más barata que la de la intercalación de la base: sobre el millón de miembros, el correo bajó de 2,4–2,7 s a
    /// 0,4–0,6 s y el apellido de 0,6–1,1 s a 0,16–0,23 s. Las mayúsculas a los dos lados mantienen la búsqueda sin
    /// distinguir mayúsculas. El valor va como parámetro, no como literal, para que SQL Server reutilice el plan.
    /// </summary>
    private static Expression FiltroBinario(ParameterExpression x, PropertyInfo prop, string op, string valor)
    {
        var columna = Expression.Property(x, prop);
        var upper   = Expression.Call(columna, typeof(string).GetMethod(nameof(string.ToUpper), Type.EmptyTypes)!);
        var collate = Expression.Call(
            typeof(RelationalDbFunctionsExtensions).GetMethod(nameof(RelationalDbFunctionsExtensions.Collate))!
                .MakeGenericMethod(typeof(string)),
            Expression.Constant(EF.Functions),
            upper,
            Expression.Constant(IntercalacionBinaria));

        var buscado = Expression.Property(Expression.Constant(new ValorBuscado(valor.Trim().ToUpperInvariant())),
            nameof(ValorBuscado.Valor));
        var metodo  = op == "endswith" ? nameof(string.EndsWith) : nameof(string.Contains);
        var compara = Expression.Call(collate, typeof(string).GetMethod(metodo, new[] { typeof(string) })!, buscado);

        return Expression.AndAlso(Expression.NotEqual(columna, Expression.Constant(null, typeof(string))), compara);
    }

    /// <summary>Envoltorio para que EF trate el valor buscado como parámetro y no como literal en el SQL.</summary>
    private sealed record ValorBuscado(string Valor);

    /// <summary>
    /// Saca de la petición los filtros "igual a" de <paramref name="field"/> y devuelve sus valores, para que quien
    /// llama los aplique sobre la entidad antes de proyectar. Sirve para las columnas que el DTO enseña como texto pero
    /// que en la tabla son un enum (Status.ToString()): filtrar sobre la proyección es un CASE por fila que ningún
    /// índice resuelve; sobre la entidad es una comparación de entero. Solo los saca cuando todos los filtros se unen
    /// con "and": con un "or" de por medio, aplicarlos aparte cambiaría el resultado, y se quedan donde estaban.
    /// </summary>
    public static bool TryTakeEqualFilters(this GridDataRequest request, string field, out List<string> values)
    {
        values = new List<string>();
        if (request.Filters is null || request.Filters.Count == 0) return false;
        if (request.Filters.Any(f => string.Equals(f.Logic, "or", StringComparison.OrdinalIgnoreCase))) return false;

        var propios = request.Filters
            .Where(f => string.Equals(f.Field, field, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(f.Operator, "equal", StringComparison.OrdinalIgnoreCase)
                     && f.Value is not null)
            .ToList();
        if (propios.Count == 0) return false;

        foreach (var f in propios) request.Filters.Remove(f);
        values = propios.Select(f => f.Value!).ToList();
        return true;
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
