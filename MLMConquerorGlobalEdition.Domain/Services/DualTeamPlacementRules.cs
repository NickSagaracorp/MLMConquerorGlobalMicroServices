using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Services;

/// <summary>
/// Pure, dependency-free decision logic for dual-team binary placement (deepest-chain
/// spillover). Kept out of the infrastructure service so the rules can be unit-tested in
/// isolation and so every placement writer shares one definition.
///
/// Rules:
///   a) sponsor has no left child   → place LEFT under sponsor
///   b) sponsor has left, no right  → place RIGHT under sponsor
///   c) sponsor has both children   → spill onto the deepest open node of the preferred
///      side, stacking on its first open side (Left first → builds the chain).
/// </summary>
public static class DualTeamPlacementRules
{
    public readonly record struct SlotDecision(string ParentMemberId, TreeSide Side, bool IsSpill);

    /// <summary>The side a sponsor spills onto: its own position (Left if it is a root).</summary>
    public static TreeSide PreferredSide(bool sponsorIsRoot, TreeSide sponsorSide)
        => sponsorIsRoot ? TreeSide.Left : sponsorSide;

    /// <summary>
    /// Decide the slot for a new member under <paramref name="sponsorMemberId"/>.
    /// For spillover, <paramref name="deepestNodeId"/> is the cached chain tail and
    /// <paramref name="deepestNodeHasLeftChild"/> selects its open side.
    /// </summary>
    public static SlotDecision Decide(
        string sponsorMemberId,
        bool sponsorHasLeftChild,
        bool sponsorHasRightChild,
        string? deepestNodeId,
        bool deepestNodeHasLeftChild)
    {
        if (!sponsorHasLeftChild) return new SlotDecision(sponsorMemberId, TreeSide.Left, false);
        if (!sponsorHasRightChild) return new SlotDecision(sponsorMemberId, TreeSide.Right, false);

        var parent = deepestNodeId ?? sponsorMemberId;
        var side = deepestNodeHasLeftChild ? TreeSide.Right : TreeSide.Left;
        return new SlotDecision(parent, side, true);
    }

    /// <summary>
    /// Whether this placement extends the sponsor's preferred-side chain (and therefore
    /// advances the frontier): a spill, or a direct child landing on the preferred side.
    /// </summary>
    public static bool ExtendsPreferredSide(SlotDecision decision, string sponsorMemberId, TreeSide preferredSide)
        => decision.IsSpill || (decision.ParentMemberId == sponsorMemberId && decision.Side == preferredSide);

    /// <summary>Depth indicator = slash count of the materialized path ('/A/' = 1, '/A/B/' = 2).</summary>
    public static int Depth(string hierarchyPath) => hierarchyPath.Count(c => c == '/');

    /// <summary>Build a child's materialized path, guaranteeing exactly one separator.</summary>
    public static string ChildPath(string parentPath, string memberId)
        => $"{parentPath.TrimEnd('/')}/{memberId}/";
}
