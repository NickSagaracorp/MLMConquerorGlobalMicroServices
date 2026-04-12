using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.RankEngine.Features.GetRankDefinitions;
using MLMConquerorGlobalEdition.RankEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Features;

public class GetRankDefinitionsHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Mock<ICacheService> BuildCache(List<DTOs.RankDefinitionResponse>? hit = null)
    {
        var m = new Mock<ICacheService>();
        m.Setup(c => c.GetAsync<List<DTOs.RankDefinitionResponse>>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hit);
        m.Setup(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<List<DTOs.RankDefinitionResponse>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return m;
    }

    private static RankDefinition BuildRank(int id, int sortOrder, bool active = true) => new()
    {
        Id           = id,
        Name         = $"Rank-{sortOrder}",
        SortOrder    = sortOrder,
        Status       = active ? RankDefinitionStatus.Active : RankDefinitionStatus.Inactive,
        CreatedBy    = "seed",
        CreationDate = FixedNow
    };

    [Fact]
    public async Task Handle_WhenCacheHit_ReturnsCachedListWithoutHittingDb()
    {
        await using var db = InMemoryDbHelper.Create();
        var cachedList = new List<DTOs.RankDefinitionResponse>
        {
            new() { Id = 99, Name = "CachedRank", SortOrder = 9 }
        };
        var cache = BuildCache(hit: cachedList);

        var handler = new GetRankDefinitionsHandler(db, cache.Object);
        var result  = await handler.Handle(new GetRankDefinitionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Id.Should().Be(99);
        // DB was not queried — no ranks seeded and result is still non-empty
    }

    [Fact]
    public async Task Handle_WhenCacheMiss_QueriesDbAndReturnsActiveRanks()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.RankDefinitions.AddRangeAsync(
            BuildRank(1, sortOrder: 1),
            BuildRank(2, sortOrder: 2),
            BuildRank(3, sortOrder: 3, active: false));
        await db.SaveChangesAsync();
        var cache = BuildCache(hit: null);

        var handler = new GetRankDefinitionsHandler(db, cache.Object);
        var result  = await handler.Handle(new GetRankDefinitionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
        result.Value.Select(r => r.SortOrder).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Handle_WhenCacheMiss_StoresResultInCache()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.RankDefinitions.AddAsync(BuildRank(1, sortOrder: 1));
        await db.SaveChangesAsync();
        var cache = BuildCache(hit: null);

        var handler = new GetRankDefinitionsHandler(db, cache.Object);
        await handler.Handle(new GetRankDefinitionsQuery(), CancellationToken.None);

        cache.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<List<DTOs.RankDefinitionResponse>>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoActiveRanks_ReturnsEmptyList()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.RankDefinitions.AddAsync(BuildRank(1, sortOrder: 1, active: false));
        await db.SaveChangesAsync();
        var cache = BuildCache(hit: null);

        var handler = new GetRankDefinitionsHandler(db, cache.Object);
        var result  = await handler.Handle(new GetRankDefinitionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }
}
