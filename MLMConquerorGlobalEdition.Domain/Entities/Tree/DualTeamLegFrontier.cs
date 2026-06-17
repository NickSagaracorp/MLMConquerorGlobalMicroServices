using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tree;

/// <summary>
/// Caches the current deepest node ("frontier") of a sponsor's preferred-side spillover
/// chain so deepest-on-side placement resolves the target in O(1) instead of an
/// O(subtree) BFS. One row per sponsor that has spilled at least once. The cache is
/// advisory: placement self-heals (descends the preferred side) if the cached tail turns
/// out to be stale.
/// </summary>
public class DualTeamLegFrontier : AuditChangesStringKey
{
    /// <summary>The sponsor whose preferred-side spill chain this row tracks.</summary>
    public string SponsorMemberId { get; set; } = string.Empty;

    /// <summary>The side the sponsor spills onto (the sponsor's own position; Left for a root).</summary>
    public TreeSide PreferredSide { get; set; }

    /// <summary>Current deepest node of the preferred-side chain (null until the first spill).</summary>
    public string? DeepestMemberId { get; set; }

    /// <summary>Slash-count depth of <see cref="DeepestMemberId"/>'s HierarchyPath.</summary>
    public int DeepestDepth { get; set; }
}
