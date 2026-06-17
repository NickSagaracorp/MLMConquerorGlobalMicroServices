using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Helpers;

public class RankScenarioBuilderTests
{
    // ── Test 1: EnsureCreated seeds the canonical 19 RankRequirement rows ──────

    [Fact]
    public async Task EnsureCreated_SeedsTheCanonicalRankRequirements()
    {
        await using var db = InMemoryDbHelper.Create();
        db.Database.EnsureCreated();

        // RankDefinitionConfiguration seeds 20 definitions (incl. Lifestyle Consultant),
        // but only ranks 1-19 have a RankRequirement row.
        (await db.RankDefinitions.CountAsync()).Should().BeGreaterThanOrEqualTo(19);
        (await db.RankRequirements.CountAsync()).Should().Be(19);
    }

    // ── Test 2: Low rank (Silver = Id 1) — subject qualifies ──────────────────

    [Fact]
    public async Task BuildForRank_LowRank_SubjectQualifies()
    {
        await using var db = InMemoryDbHelper.Create();
        var builder = new RankScenarioBuilder(db);
        var subjectId = await builder.BuildForRankAsync(rankDefinitionId: 1);

        // Subject MemberId must use the "RVH-" prefix.
        subjectId.Should().StartWith(RankScenarioBuilder.MemberPrefix);

        var requirement = db.RankRequirements
            .Single(r => r.RankDefinitionId == 1);

        var svc = new RankQualificationService(
            db,
            new EnrollmentTeamPointsService(db),
            new PersonalCustomerPointsService(db));

        var result = await svc.QualifiesForRankAsync(subjectId, requirement);

        result.Qualifies.Should().BeTrue(
            "the builder should produce a member who satisfies every axis for rank 1 (Silver)");
    }

    // ── Test 3: High rank (Black Royal = Id 19) — subject qualifies ───────────

    [Fact]
    public async Task BuildForRank_HighRank_SubjectQualifies()
    {
        await using var db = InMemoryDbHelper.Create();
        var builder = new RankScenarioBuilder(db);
        var subjectId = await builder.BuildForRankAsync(rankDefinitionId: 19);

        var requirement = db.RankRequirements
            .Single(r => r.RankDefinitionId == 19);

        var svc = new RankQualificationService(
            db,
            new EnrollmentTeamPointsService(db),
            new PersonalCustomerPointsService(db));

        var result = await svc.QualifiesForRankAsync(subjectId, requirement);

        // Verify each axis individually to aid debugging if this fails.
        result.MeetsGate.Should().BeTrue("gate: 3+ sponsored with 9+ PCP");
        result.MeetsDualTeam.Should().BeTrue("DT: both legs at ceil(700000/2) satisfies 700000 DT threshold");
        result.MeetsEnrollmentTeam.Should().BeTrue("ET: two branches each at ceil(350000/2) satisfies 350000 ET threshold");
        result.MeetsExternalMembers.Should().BeTrue("ExternalMembers axis is opted out at the seed (threshold = 0) — every member passes");
        result.MeetsPersonalPoints.Should().BeTrue("subject PersonalPoints = Max(1, requirement.PersonalPoints)");
        result.Qualifies.Should().BeTrue(
            "the builder must produce a member who satisfies every axis for rank 19 (Black Royal)");
    }

    // ── Test 4: Rank 4 (Titanium) with thresholdDelta = -1 — must NOT promote ─

    [Fact]
    public async Task BuildForRank_WithNegativeThresholdDelta_SubjectDoesNotQualify()
    {
        await using var db = InMemoryDbHelper.Create();
        var builder = new RankScenarioBuilder(db);
        var subjectId = await builder.BuildForRankAsync(rankDefinitionId: 4, thresholdDelta: -1);

        // Rank 4 (Titanium): ET = 175. Delta -1 → etTarget = 174.
        // Each branch gets ceil(174/2) = 87 points.
        // Per-branch cap = round(0.5 × 175) = 88 (round-half-to-even of 87.5).
        // Eligible ET = min(87,88) + min(87,88) = 174 < 175 → ET axis fails.
        var requirement = db.RankRequirements
            .Single(r => r.RankDefinitionId == 4);

        var svc = new RankQualificationService(
            db,
            new EnrollmentTeamPointsService(db),
            new PersonalCustomerPointsService(db));

        var result = await svc.QualifiesForRankAsync(subjectId, requirement);

        result.Qualifies.Should().BeFalse(
            "one ET point below the rank-4 threshold must block promotion");
    }
}
