namespace MLMConquerorGlobalEdition.Repository.Grid;

/// <summary>
/// Framework-neutral description of a server-side grid read: paging + a free-text
/// search + per-column filters + multi-column sort. The Syncfusion CustomAdaptor on
/// the client maps its <c>DataManagerRequest</c> onto this shape and POSTs it, so the
/// API never takes a dependency on Syncfusion types. Applied to any <see cref="IQueryable{T}"/>
/// via <see cref="GridQueryableExtensions.ToGridResultAsync{T}"/>.
/// </summary>
public class GridDataRequest
{
    /// <summary>1-based page number.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Rows per page. Clamped server-side to a safe maximum.</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>Free-text term matched (case-insensitive, contains) across the
    /// caller-supplied searchable string columns.</summary>
    public string? Search { get; set; }

    /// <summary>Per-column sort directives, applied in order.</summary>
    public List<GridSort> Sorts { get; set; } = new();

    /// <summary>Per-column filter clauses, combined left-to-right using each
    /// clause's <see cref="GridFilter.Logic"/>.</summary>
    public List<GridFilter> Filters { get; set; } = new();
}

/// <summary>A single column sort directive.</summary>
public class GridSort
{
    public string Field { get; set; } = string.Empty;

    /// <summary>"asc" (default) or "desc".</summary>
    public string Direction { get; set; } = "asc";
}

/// <summary>A single column filter clause.</summary>
public class GridFilter
{
    public string Field { get; set; } = string.Empty;

    /// <summary>contains | startswith | endswith | equal | notequal |
    /// greaterthan | greaterthanorequal | lessthan | lessthanorequal.</summary>
    public string Operator { get; set; } = "contains";

    /// <summary>Comparison value as a string; coerced to the target property type.</summary>
    public string? Value { get; set; }

    /// <summary>"and" (default) or "or" — how this clause joins the previous one.</summary>
    public string Logic { get; set; } = "and";
}
