using FluentAssertions;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;

namespace MLMConquerorGlobalEdition.Domain.Tests;

/// <summary>
/// Unit tests for the pure dual-team deepest-chain placement decision logic. Covers the
/// three placement rules, frontier advancement, and the path/depth helpers.
/// </summary>
public class DualTeamPlacementRulesTests
{
    private const string Sponsor = "AMB-SPON";
    private const string Tail    = "AMB-TAIL";

    // ── Rule a: sponsor has no left child → place LEFT under sponsor ──────────
    [Fact]
    public void Decide_WhenSponsorHasNoLeftChild_PlacesLeftUnderSponsor()
    {
        var d = DualTeamPlacementRules.Decide(Sponsor, sponsorHasLeftChild: false, sponsorHasRightChild: false, deepestNodeId: null, deepestNodeHasLeftChild: false);
        d.ParentMemberId.Should().Be(Sponsor);
        d.Side.Should().Be(TreeSide.Left);
        d.IsSpill.Should().BeFalse();
    }

    // ── Rule b: sponsor has left only → place RIGHT under sponsor ─────────────
    [Fact]
    public void Decide_WhenSponsorHasLeftButNoRight_PlacesRightUnderSponsor()
    {
        var d = DualTeamPlacementRules.Decide(Sponsor, sponsorHasLeftChild: true, sponsorHasRightChild: false, deepestNodeId: null, deepestNodeHasLeftChild: false);
        d.ParentMemberId.Should().Be(Sponsor);
        d.Side.Should().Be(TreeSide.Right);
        d.IsSpill.Should().BeFalse();
    }

    // ── Rule c: both children → spill to the deepest node (chain tail) ────────
    [Fact]
    public void Decide_WhenSponsorFull_SpillsToDeepestNodeLeftFirst()
    {
        var d = DualTeamPlacementRules.Decide(Sponsor, sponsorHasLeftChild: true, sponsorHasRightChild: true, deepestNodeId: Tail, deepestNodeHasLeftChild: false);
        d.ParentMemberId.Should().Be(Tail);
        d.Side.Should().Be(TreeSide.Left);
        d.IsSpill.Should().BeTrue();
    }

    [Fact]
    public void Decide_WhenSponsorFullAndTailHasLeftChild_SpillsRight()
    {
        var d = DualTeamPlacementRules.Decide(Sponsor, sponsorHasLeftChild: true, sponsorHasRightChild: true, deepestNodeId: Tail, deepestNodeHasLeftChild: true);
        d.ParentMemberId.Should().Be(Tail);
        d.Side.Should().Be(TreeSide.Right);
        d.IsSpill.Should().BeTrue();
    }

    [Fact]
    public void Decide_WhenSponsorFullButNoCachedTail_FallsBackToSponsor()
    {
        // Defensive: spill requested with no frontier → parent defaults to sponsor.
        var d = DualTeamPlacementRules.Decide(Sponsor, sponsorHasLeftChild: true, sponsorHasRightChild: true, deepestNodeId: null, deepestNodeHasLeftChild: false);
        d.ParentMemberId.Should().Be(Sponsor);
        d.IsSpill.Should().BeTrue();
    }

    // ── PreferredSide ────────────────────────────────────────────────────────
    [Fact]
    public void PreferredSide_WhenSponsorIsRoot_IsLeft()
        => DualTeamPlacementRules.PreferredSide(sponsorIsRoot: true, sponsorSide: TreeSide.Right).Should().Be(TreeSide.Left);

    [Theory]
    [InlineData(TreeSide.Left)]
    [InlineData(TreeSide.Right)]
    public void PreferredSide_WhenSponsorNotRoot_IsSponsorOwnSide(TreeSide side)
        => DualTeamPlacementRules.PreferredSide(sponsorIsRoot: false, sponsorSide: side).Should().Be(side);

    // ── ExtendsPreferredSide (frontier advancement) ──────────────────────────
    [Fact]
    public void ExtendsPreferredSide_WhenSpill_IsTrue()
    {
        var d = new DualTeamPlacementRules.SlotDecision(Tail, TreeSide.Left, IsSpill: true);
        DualTeamPlacementRules.ExtendsPreferredSide(d, Sponsor, TreeSide.Left).Should().BeTrue();
    }

    [Fact]
    public void ExtendsPreferredSide_WhenDirectChildOnPreferredSide_IsTrue()
    {
        var d = new DualTeamPlacementRules.SlotDecision(Sponsor, TreeSide.Left, IsSpill: false);
        DualTeamPlacementRules.ExtendsPreferredSide(d, Sponsor, TreeSide.Left).Should().BeTrue();
    }

    [Fact]
    public void ExtendsPreferredSide_WhenDirectChildOnNonPreferredSide_IsFalse()
    {
        var d = new DualTeamPlacementRules.SlotDecision(Sponsor, TreeSide.Right, IsSpill: false);
        DualTeamPlacementRules.ExtendsPreferredSide(d, Sponsor, TreeSide.Left).Should().BeFalse();
    }

    // ── Depth ────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("/A/", 2)]
    [InlineData("/A/B/", 3)]
    [InlineData("/A/B/C/", 4)]
    public void Depth_CountsSlashes(string path, int expected)
        => DualTeamPlacementRules.Depth(path).Should().Be(expected);

    // ── ChildPath ──────────────────────────────────────────────────────────--
    [Fact]
    public void ChildPath_AppendsWithSingleSeparator()
        => DualTeamPlacementRules.ChildPath("/A/B/", "C").Should().Be("/A/B/C/");

    [Fact]
    public void ChildPath_WhenParentMissingTrailingSlash_StillSingleSeparator()
        => DualTeamPlacementRules.ChildPath("/A/B", "C").Should().Be("/A/B/C/");

    // ── Chain advancement: simulate stacking under a sponsor ─────────────────-
    [Fact]
    public void Decide_DeepestChain_AdvancesDownLeftSpine()
    {
        // After both direct children exist, repeated spills stack Left, each new node
        // becoming the next tail with no left child of its own → always Left.
        var tail = "n0";
        for (var i = 1; i <= 5; i++)
        {
            var d = DualTeamPlacementRules.Decide(Sponsor, true, true, tail, deepestNodeHasLeftChild: false);
            d.IsSpill.Should().BeTrue();
            d.ParentMemberId.Should().Be(tail);
            d.Side.Should().Be(TreeSide.Left);
            tail = $"n{i}"; // new tail
        }
    }
}
