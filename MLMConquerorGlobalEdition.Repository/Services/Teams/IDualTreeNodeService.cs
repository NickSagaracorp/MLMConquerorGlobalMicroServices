namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// Single source of truth for the dual-tree (binary-tree) visualization payload.
/// Used by both BizCenter (own profile) and Admin (member profile drill-down).
/// Do NOT duplicate this query/mapping logic anywhere else.
/// </summary>
public interface IDualTreeNodeService
{
    /// <summary>
    /// Returns the rooted view of <paramref name="rootMemberId"/> with up to two
    /// levels of descendants populated, plus boolean flags for whether deeper
    /// great-grandchildren exist.
    /// </summary>
    Task<DualTreeNodeView> GetNodeAsync(string rootMemberId, CancellationToken ct = default);
}
