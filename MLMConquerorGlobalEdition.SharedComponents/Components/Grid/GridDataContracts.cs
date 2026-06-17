namespace MLMConquerorGlobalEdition.SharedComponents.Components.Grid;

/// <summary>
/// Wire contract for the server-side grid endpoints (e.g. <c>POST api/v1/admin/members/grid</c>).
/// Mirrors the backend <c>GridDataRequest</c> shape exactly. Lives in SharedComponents so every
/// server-side grid (Members today; Dual Team / Enrollment next) serialises the same body.
/// </summary>
public sealed class GridDataRequest
{
    /// <summary>1-based page index.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Rows per page. Components cap this (Members caps at 100).</summary>
    public int PageSize { get; set; } = 50;

    /// <summary>Free-text search; server does case-insensitive contains across string columns.</summary>
    public string? Search { get; set; }

    /// <summary>Sort directives in the order the user applied them.</summary>
    public List<GridSortDescriptor> Sorts { get; set; } = new();

    /// <summary>Column filter predicates, flattened from Syncfusion's WhereFilter tree.</summary>
    public List<GridFilterDescriptor> Filters { get; set; } = new();
}

/// <summary>One sort directive. <see cref="Direction"/> is "asc" or "desc".</summary>
public sealed class GridSortDescriptor
{
    public string Field { get; set; } = string.Empty;
    public string Direction { get; set; } = "asc";
}

/// <summary>
/// One column filter predicate. Operator names match the backend contract one-to-one
/// (contains / startswith / endswith / equal / notequal / greaterthan / greaterthanorequal /
/// lessthan / lessthanorequal). <see cref="Logic"/> is "and" or "or".
/// </summary>
public sealed class GridFilterDescriptor
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "equal";
    public string? Value { get; set; }
    public string Logic { get; set; } = "and";
}

/// <summary>Server page envelope. Matches SharedKernel.PagedResult JSON shape over the wire.</summary>
public sealed class GridPagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>Standard API envelope. Mirrors SharedKernel.ApiResponse JSON shape.</summary>
public sealed class GridApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public IEnumerable<string>? Errors { get; set; }
}
