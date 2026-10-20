namespace MLMConquerorGlobalEdition.SharedKernel;

public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Plan Q (DQ7): el equipo supero TeamGridLimits.FreeMaxRows, asi que el orden es por estructura y la busqueda por
    /// prefijo. La rejilla lo avisa.
    /// </summary>
    public bool Limited { get; set; }
}
