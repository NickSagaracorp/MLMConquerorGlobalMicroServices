using System.Linq;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.RankEngine.Features.EvaluateRank;
using MLMConquerorGlobalEdition.RankEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Features;

public class RankReachabilityTests
{
    /// <summary>RankDefinition.Id values for the 19 progression ranks (Silver..Black Royal).</summary>
    public static IEnumerable<object[]> AllRanks =>
        Enumerable.Range(1, 19).Select(id => new object[] { id });

    [Theory]
    [MemberData(nameof(AllRanks))]
    public async Task EveryRank_IsReachable_WhenItsRequirementsAreMet(int rankDefinitionId)
    {
        await using var db = InMemoryDbHelper.Create();
        var subjectId = await new RankScenarioBuilder(db).BuildForRankAsync(rankDefinitionId);

        var result = await RankReachabilityTestHandlerFactory.Build(db)
            .Handle(new EvaluateRankCommand(subjectId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RankAchieved.Should().BeTrue(
            $"rank {rankDefinitionId} must be reachable when its requirements + the gate are met");
        result.Value.AchievedRank!.Id.Should().Be(rankDefinitionId,
            $"rank {rankDefinitionId} must be the rank achieved");
    }

    [Theory]
    [MemberData(nameof(AllRanks))]
    public async Task EveryRank_IsNotReached_WhenThresholdsAreOneShort(int rankDefinitionId)
    {
        await using var db = InMemoryDbHelper.Create();
        var subjectId = await new RankScenarioBuilder(db).BuildForRankAsync(rankDefinitionId, thresholdDelta: -1);

        var result = await RankReachabilityTestHandlerFactory.Build(db)
            .Handle(new EvaluateRankCommand(subjectId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // One point short of rank N's thresholds: the member must NOT reach rank N.
        // They may still hold a LOWER rank (their points exceed every lower rank), or
        // none at all — both are acceptable, so the assertion is guarded by RankAchieved.
        if (result.Value!.RankAchieved)
            result.Value.AchievedRank!.SortOrder.Should().BeLessThan(rankDefinitionId,
                $"one point short of rank {rankDefinitionId}'s thresholds must not reach it");
    }

    [Fact]
    public async Task Ladder_MemberWithTopRankData_ClimbsToBlackRoyal_AndReEvaluationIsIdempotent()
    {
        // A member built with Black Royal (rank 19) data is promoted by the real
        // EvaluateRankHandler to the highest qualifying rank in one evaluation.
        // Per-rank reachability of every rung is covered by EveryRank_IsReachable_WhenItsRequirementsAreMet.
        await using var db = InMemoryDbHelper.Create();
        var subjectId = await new RankScenarioBuilder(db).BuildForRankAsync(rankDefinitionId: 19);
        var handler = RankReachabilityTestHandlerFactory.Build(db);

        var first = await handler.Handle(new EvaluateRankCommand(subjectId), CancellationToken.None);
        first.IsSuccess.Should().BeTrue();
        first.Value!.RankAchieved.Should().BeTrue("a member meeting every Black Royal requirement must be promoted");
        first.Value.AchievedRank!.Id.Should().Be(19, "a member meeting Black Royal's requirements must reach it");

        // Re-evaluating must not promote again or error — there is no rank above 19.
        var second = await handler.Handle(new EvaluateRankCommand(subjectId), CancellationToken.None);
        second.IsSuccess.Should().BeTrue();
        second.Value!.RankAchieved.Should().BeFalse("no rank exists above Black Royal");
    }

    [Fact]
    public async Task Gate_WhenPcpBelowTwelveAndSponsoredMembershipsInactive_RankNotAchieved()
    {
        await using var db = InMemoryDbHelper.Create();
        var subjectId = await new RankScenarioBuilder(db).BuildForRankAsync(rankDefinitionId: 1);

        // Break the gate: deactivate every sponsored member's membership and drop the
        // subject's own membership points to 5.
        // sponsored members' memberships deactivated → their PCP contribution is 0; the sponsored-member count itself stays 3.
        foreach (var sub in db.MembershipSubscriptions)
            if (sub.MemberId != subjectId)
                sub.SubscriptionStatus = MembershipStatus.Cancelled;
        // RankScenarioBuilder names each member's membership product "PRD-{memberId}".
        db.Products.Single(p => p.Id == $"PRD-{subjectId}").QualificationPoins = 5;
        await db.SaveChangesAsync();

        var result = await RankReachabilityTestHandlerFactory.Build(db)
            .Handle(new EvaluateRankCommand(subjectId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RankAchieved.Should().BeFalse("the universal gate must block promotion");
    }

    [Fact]
    public async Task Gate_WhenTwelvePcpAndSponsoredMembershipsInactive_RankAchieved()
    {
        await using var db = InMemoryDbHelper.Create();
        var subjectId = await new RankScenarioBuilder(db).BuildForRankAsync(rankDefinitionId: 1);

        // sponsored members' memberships deactivated → their PCP contribution is 0; the sponsored-member count itself stays 3.
        // The subject's own membership is worth 12 PCP, which satisfies the gate alone.
        foreach (var sub in db.MembershipSubscriptions)
            if (sub.MemberId != subjectId)
                sub.SubscriptionStatus = MembershipStatus.Cancelled;
        // RankScenarioBuilder names each member's membership product "PRD-{memberId}".
        db.Products.Single(p => p.Id == $"PRD-{subjectId}").QualificationPoins = 12;
        await db.SaveChangesAsync();

        var result = await RankReachabilityTestHandlerFactory.Build(db)
            .Handle(new EvaluateRankCommand(subjectId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RankAchieved.Should().BeTrue("12 PCP alone satisfies the universal gate");
    }

    /// <summary>
    /// Spec §6: the BizCenter dashboard (RankComputationService) and the promotion engine
    /// (EvaluateRankHandler) must produce the same qualification verdict for the same member data.
    /// The dashboard is measured BEFORE the engine writes its MemberRankHistory row so the
    /// live computation is independent of history.
    /// </summary>
    [Fact]
    public async Task DashboardAndEngine_AgreeOnAchievedRank()
    {
        const int TargetRankId = 8; // Ruby — mid-ladder rank

        await using var db = InMemoryDbHelper.Create();
        var subjectId = await new RankScenarioBuilder(db).BuildForRankAsync(TargetRankId);

        // --- Dashboard: compute the current qualifying rank live from points (no history yet) ---
        var et      = new EnrollmentTeamPointsService(db);
        var pcp     = new PersonalCustomerPointsService(db);
        var qual    = new RankQualificationService(db, et, pcp);
        var svc     = new RankComputationService(db, qual);
        var summary = await svc.GetSummaryAsync(subjectId);

        // --- Engine: evaluate and promote (writes MemberRankHistory) ---
        var result = await RankReachabilityTestHandlerFactory.Build(db)
            .Handle(new EvaluateRankCommand(subjectId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RankAchieved.Should().BeTrue(
            because: "the engine must promote a member whose data meets rank-8 requirements");

        summary.CurrentRankId.Should().Be(TargetRankId,
            because: "the dashboard and the promotion engine must report the same rank for the same data");
        result.Value.AchievedRank!.Id.Should().Be(TargetRankId,
            because: "the dashboard and the promotion engine must report the same rank for the same data");
        summary.CurrentRankId.Should().Be(result.Value.AchievedRank.Id,
            because: "the dashboard and the promotion engine must report the same rank for the same data");
    }
}
