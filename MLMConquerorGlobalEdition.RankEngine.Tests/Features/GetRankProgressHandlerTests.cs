using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.RankEngine.Features.GetRankProgress;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.RankEngine.Tests.Helpers;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Features;

public class GetRankProgressHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> BuildClock() =>
        new Mock<IDateTimeProvider>().Also(m => m.Setup(d => d.Now).Returns(FixedNow));

    private static MemberProfile BuildMember(string memberId) => new()
    {
        MemberId       = memberId,
        FirstName      = "Test",
        LastName       = "User",
        MemberType     = MemberType.Ambassador,
        EnrollDate     = FixedNow.AddMonths(-6),
        Country        = "US",
        CreatedBy      = "seed",
        LastUpdateDate = FixedNow
    };

    private static RankDefinition BuildRank(int id, int sortOrder) => new()
    {
        Id           = id,
        Name         = $"Rank-{sortOrder}",
        SortOrder    = sortOrder,
        Status       = RankDefinitionStatus.Active,
        CreatedBy    = "seed",
        CreationDate = FixedNow,
        Requirements = new List<RankRequirement>
        {
            new()
            {
                Id               = id * 100,
                RankDefinitionId = id,
                LevelNo          = 0,
                PersonalPoints   = sortOrder * 10,
                SponsoredMembers = 1,
                ExternalMembers  = 1,
                CreatedBy        = "seed",
                CreationDate     = FixedNow
            }
        }
    };

    private static MemberRankHistory BuildHistory(string memberId, int rankId, int rankSortOrder) => new()
    {
        MemberId         = memberId,
        RankDefinitionId = rankId,
        AchievedAt       = FixedNow.AddMonths(-1),
        CreatedBy        = "seed",
        CreationDate     = FixedNow.AddMonths(-1),
        LastUpdateDate   = FixedNow.AddMonths(-1)
    };

    [Fact]
    public async Task Handle_WhenMemberNotFound_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new GetRankProgressHandler(db, BuildClock().Object);

        var result = await handler.Handle(
            new GetRankProgressQuery("AMB-GHOST"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenMemberHasNoRankHistory_CurrentRankIsNull()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        await db.RankDefinitions.AddAsync(BuildRank(1, sortOrder: 1));
        await db.SaveChangesAsync();

        var handler = new GetRankProgressHandler(db, BuildClock().Object);
        var result  = await handler.Handle(new GetRankProgressQuery("AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CurrentRank.Should().BeNull();
        result.Value.NextRank.Should().NotBeNull();
        result.Value.NextRank!.SortOrder.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenMemberHasRank_CurrentRankPopulatedAndNextRankIsHigher()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        var rank1 = BuildRank(1, sortOrder: 1);
        var rank2 = BuildRank(2, sortOrder: 2);
        await db.RankDefinitions.AddRangeAsync(rank1, rank2);
        await db.SaveChangesAsync();

        await db.MemberRankHistories.AddAsync(BuildHistory("AMB-001", rankId: 1, rankSortOrder: 1));
        await db.SaveChangesAsync();

        var handler = new GetRankProgressHandler(db, BuildClock().Object);
        var result  = await handler.Handle(new GetRankProgressQuery("AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CurrentRank.Should().NotBeNull();
        result.Value.CurrentRank!.SortOrder.Should().Be(1);
        result.Value.NextRank.Should().NotBeNull();
        result.Value.NextRank!.SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenMemberAtHighestRank_NextRankIsNull()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        await db.RankDefinitions.AddAsync(BuildRank(1, sortOrder: 1));
        await db.SaveChangesAsync();

        await db.MemberRankHistories.AddAsync(BuildHistory("AMB-001", rankId: 1, rankSortOrder: 1));
        await db.SaveChangesAsync();

        var handler = new GetRankProgressHandler(db, BuildClock().Object);
        var result  = await handler.Handle(new GetRankProgressQuery("AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.NextRank.Should().BeNull();
        result.Value.ProgressToNextRank.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenHasDualTreeNode_PopulatesTeamPoints()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        await db.DualTeamTree.AddAsync(new DualTeamEntity
        {
            MemberId       = "AMB-001",
            LeftLegPoints  = 300,
            RightLegPoints = 200,
            HierarchyPath  = "/AMB-001/",
            CreatedBy      = "seed",
            CreationDate   = FixedNow,
            LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var handler = new GetRankProgressHandler(db, BuildClock().Object);
        var result  = await handler.Handle(new GetRankProgressQuery("AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CurrentMetrics.LeftLegPoints.Should().Be(300);
        result.Value.CurrentMetrics.RightLegPoints.Should().Be(200);
        // Qualifying = weaker leg × 2 = 200 × 2 = 400
        result.Value.CurrentMetrics.QualifyingTeamPoints.Should().Be(400);
    }

    [Fact]
    public async Task Handle_ProgressToNextRankIsComputed_WhenNextRankExists()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        // Rank 1 requires PersonalPoints=10, SponsoredMembers=1, ExternalMembers=1
        await db.RankDefinitions.AddAsync(BuildRank(1, sortOrder: 1));
        await db.SaveChangesAsync();

        var handler = new GetRankProgressHandler(db, BuildClock().Object);
        var result  = await handler.Handle(new GetRankProgressQuery("AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ProgressToNextRank.Should().NotBeNull();
        // 0 points / 10 required = 0%
        result.Value.ProgressToNextRank!.PersonalPointsPercent.Should().Be(0);
    }
}

// Helper extension to allow .Also(configure) inline for Mock setup
internal static class MockExtensions
{
    internal static T Also<T>(this T obj, Action<T> configure)
    {
        configure(obj);
        return obj;
    }
}
