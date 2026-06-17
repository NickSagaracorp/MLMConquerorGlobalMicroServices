using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Syncfusion.Blazor;
using Syncfusion.Blazor.Data;

namespace MLMConquerorGlobalEdition.SharedComponents.Components.Grid;

/// <summary>
/// Reusable Syncfusion <see cref="DataAdaptor"/> that pushes ALL grid operations
/// (search, column filters, sort, paging) to a server endpoint and returns one page
/// at a time. Because the server owns paging/filtering/sorting, the grid can search
/// across <b>every</b> record — not just the rows currently materialised on the client.
///
/// <para>
/// Designed for reuse across every server-side grid in the platform (Admin Members
/// today; Dual Team / Enrollment Team next). The host component configures it per
/// instance through parameters rather than DI, so the same class serves many grids:
/// </para>
/// <code>
/// &lt;SfGrid TValue="MyDto" @ref="_grid" AllowPaging AllowSorting AllowFiltering&gt;
///     &lt;SfDataManager&gt;
///         &lt;ServerGridAdaptor TValue="MyDto"
///                            Endpoint="api/v1/admin/members/grid"
///                            QueryStringProvider="BuildQuery" /&gt;
///     &lt;/SfDataManager&gt;
///     ...
/// &lt;/SfGrid&gt;
/// </code>
///
/// <para>
/// The endpoint must accept the <see cref="GridDataRequest"/> body via POST and return
/// <c>ApiResponse&lt;PagedResult&lt;TValue&gt;&gt;</c>.
/// </para>
/// </summary>
/// <typeparam name="TValue">The DTO type the grid binds to. Property names must match
/// the server's filter/sort <c>field</c> contract (PascalCase).</typeparam>
public class ServerGridAdaptor<TValue> : DataAdaptor
{
    /// <summary>
    /// HttpClient used to call the endpoint. If not supplied as a parameter, falls back
    /// to the injected default client (in AdminWeb the default scoped client is "AdminApi",
    /// which already attaches the JWT bearer token).
    /// </summary>
    [Parameter] public HttpClient? Http { get; set; }

    [Inject] private HttpClient InjectedHttp { get; set; } = default!;

    /// <summary>
    /// Relative endpoint path that accepts the <see cref="GridDataRequest"/> POST body,
    /// e.g. <c>api/v1/admin/members/grid</c>. Required.
    /// </summary>
    [Parameter, EditorRequired] public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Optional provider of extra query-string parameters appended to <see cref="Endpoint"/>
    /// on each read (e.g. <c>status=Active</c>, <c>bypassCache=true</c>). The host owns this
    /// closure so it can flip status / force a cache bypass between reads without the adaptor
    /// knowing the host's domain. Return the query WITHOUT a leading '?' or '&amp;'
    /// (e.g. <c>"status=Active&amp;bypassCache=true"</c>) or null/empty for none.
    /// </summary>
    [Parameter] public Func<string?>? QueryStringProvider { get; set; }

    /// <summary>
    /// Optional override for how Syncfusion's <c>dm.Search</c> maps to the request's
    /// <see cref="GridDataRequest.Search"/>. By default the first search term's <c>Key</c>
    /// is used. Most hosts drive search through <c>Grid.SearchAsync</c> instead, which routes
    /// through this same path.
    /// </summary>
    [Parameter] public Func<DataManagerRequest, string?>? SearchSelector { get; set; }

    /// <summary>
    /// Optional provider that resolves the JWT access token to attach as
    /// <c>Authorization: Bearer {token}</c> on each request. Supplied by the host
    /// component, which resolves the token from <c>[CascadingParameter] Task&lt;AuthenticationState&gt;</c>
    /// where it is reliably available — even inside Syncfusion's <see cref="ReadAsync"/> call
    /// context, where the DelegatingHandler's AuthenticationStateProvider fallback can come back
    /// empty and produce a 401.
    /// <para>
    /// A <c>Func&lt;Task&lt;string?&gt;&gt;</c> (rather than a static string) so the token is
    /// re-resolved on every read and reflects expiry/refresh. If null or it returns
    /// null/empty, the adaptor falls back to the existing behavior and lets the
    /// configured DelegatingHandler attempt to attach the token.
    /// </para>
    /// </summary>
    [Parameter] public Func<Task<string?>>? AccessTokenProvider { get; set; }

    private HttpClient Client => Http ?? InjectedHttp;

    /// <inheritdoc />
    public override async Task<object> ReadAsync(DataManagerRequest dm, string? key = null)
    {
        var request = new GridDataRequest
        {
            // Skip/Take → 1-based page. Take==0 (grid not yet sized) defaults to a safe page size.
            PageSize = dm.Take > 0 ? dm.Take : 50,
            Page     = dm.Take > 0 ? (dm.Skip / dm.Take) + 1 : 1,
            Search   = ResolveSearch(dm),
            Sorts    = MapSorts(dm.Sorted),
            Filters  = FlattenWhere(dm.Where),
        };

        var url = BuildUrl();

        // Resolve the bearer token explicitly when the host supplies a provider. Setting the
        // header on the HttpRequestMessage takes priority over the DelegatingHandler's own
        // attempt and avoids the 401 seen when the handler's AuthenticationStateProvider
        // fallback returns no token inside this read context.
        string? token = null;
        if (AccessTokenProvider is not null)
        {
            try { token = await AccessTokenProvider().ConfigureAwait(false); }
            catch { /* fall back to the handler if token resolution fails */ }
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request),
        };
        if (!string.IsNullOrEmpty(token))
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await Client.SendAsync(httpRequest).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var payload = await resp.Content
            .ReadFromJsonAsync<GridApiResponse<GridPagedResult<TValue>>>()
            .ConfigureAwait(false);

        var items = payload?.Data?.Items ?? new List<TValue>();
        var count = payload?.Data?.TotalCount ?? 0;

        // Always return Count so the server-side pager renders the correct page bar.
        // RequiresCounts is true on the operations that build the pager; returning a
        // DataResult unconditionally keeps the contract simple and correct.
        return dm.RequiresCounts
            ? new DataResult { Result = items, Count = count }
            : items;
    }

    private string BuildUrl()
    {
        var extra = QueryStringProvider?.Invoke();
        if (string.IsNullOrWhiteSpace(extra)) return Endpoint;
        var sep = Endpoint.Contains('?') ? "&" : "?";
        return $"{Endpoint}{sep}{extra.TrimStart('?', '&')}";
    }

    private string? ResolveSearch(DataManagerRequest dm)
    {
        if (SearchSelector is not null) return SearchSelector(dm);

        // Default: take the first search term Syncfusion supplies (Grid.SearchAsync routes here).
        var term = dm.Search?.FirstOrDefault();
        return term is null ? null : term.Key;
    }

    private static List<GridSortDescriptor> MapSorts(List<Sort>? sorted)
    {
        var result = new List<GridSortDescriptor>();
        if (sorted is null) return result;

        foreach (var s in sorted)
        {
            if (string.IsNullOrWhiteSpace(s.Name)) continue;
            result.Add(new GridSortDescriptor
            {
                Field     = s.Name,
                // Syncfusion sends "ascending"/"descending"; the contract wants "asc"/"desc".
                Direction = (s.Direction ?? string.Empty).StartsWith("desc", StringComparison.OrdinalIgnoreCase)
                    ? "desc" : "asc",
            });
        }
        return result;
    }

    /// <summary>
    /// Flattens Syncfusion's recursive <see cref="WhereFilter"/> tree into a flat predicate list.
    /// Excel-style filtering produces nested AND/OR groups; the server contract is a flat list
    /// where each predicate carries its own <c>logic</c>. We walk the tree depth-first and lift
    /// every leaf predicate, tagging it with the logical connector of its containing group.
    /// </summary>
    private static List<GridFilterDescriptor> FlattenWhere(List<WhereFilter>? where)
    {
        var result = new List<GridFilterDescriptor>();
        if (where is null) return result;
        foreach (var w in where) Walk(w, result);
        return result;
    }

    private static void Walk(WhereFilter w, List<GridFilterDescriptor> acc)
    {
        if (w.IsComplex && w.predicates is { Count: > 0 })
        {
            // A group node — recurse. Leaf predicates inside inherit the group's connector
            // via their own Condition; we read each leaf's Condition below.
            foreach (var child in w.predicates) Walk(child, acc);
            return;
        }

        if (string.IsNullOrWhiteSpace(w.Field)) return;

        acc.Add(new GridFilterDescriptor
        {
            Field    = w.Field,
            Operator = MapOperator(w.Operator),
            Value    = w.value?.ToString(),
            Logic    = string.IsNullOrWhiteSpace(w.Condition)
                ? "and"
                : w.Condition.Trim().ToLowerInvariant() == "or" ? "or" : "and",
        });
    }

    /// <summary>
    /// Maps Syncfusion operator tokens to the backend contract. Syncfusion already uses the
    /// same lowercase tokens for the operators this platform supports, so this is largely an
    /// identity map with normalisation and a safe default.
    /// </summary>
    private static string MapOperator(string? op) => (op ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "contains"               => "contains",
        "startswith"             => "startswith",
        "endswith"               => "endswith",
        "equal"                  => "equal",
        "notequal"               => "notequal",
        "greaterthan"            => "greaterthan",
        "greaterthanorequal"     => "greaterthanorequal",
        "lessthan"               => "lessthan",
        "lessthanorequal"        => "lessthanorequal",
        _                        => "equal",
    };
}
