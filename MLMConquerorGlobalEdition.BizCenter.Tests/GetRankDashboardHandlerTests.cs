using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Ranks;
using MLMConquerorGlobalEdition.BizCenter.Features.Ranks.GetRankDashboard;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

public class GetRankDashboardHandlerTests : IDisposable
{
    private const string MemberId = "member-rank-001";

    private readonly AppDbContext _db;
    private readonly Mock<ICurrentUserService> _currentUser;
    private readonly Mock<ICacheService> _cache;

    public GetRankDashboardHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _currentUser = new Mock<ICurrentUserService>();
        _currentUser.Setup(x => x.MemberId).Returns(MemberId);

        _cache = new Mock<ICacheService>();
        _cache.Setup(x => x.GetAsync<RankDashboardDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((RankDashboardDto?)null);
        _cache.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<RankDashboardDto>(),
                                      It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);
    }

    public void Dispose() => _db.Dispose();

    private GetRankDashboardHandler CreateHandler() =>
        new(_db, _currentUser.Object, _cache.Object);


    [Fact]
    public async Task Handle_WhenCacheHit_ReturnsCachedDtoWithoutHittingDb()
    {
        var cached = new RankDashboardDto { MemberId = MemberId, CurrentRankName = "Gold" };
        _cache.Setup(x => x.GetAsync<RankDashboardDto>(
                CacheKeys.MemberRank(MemberId), It.IsAny<CancellationToken>()))
              .ReturnsAsync(cached);

        var result = await CreateHandler().Handle(new GetRankDashboardQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CurrentRankName.Should().Be("Gold");
        _cache.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<RankDashboardDto>(),
                                       It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
                      Times.Never);
    }


    [Fact]
    public async Task Handle_WhenCacheMiss_ReturnsRankDataFromDb()
    {
        var rankDef = new RankDefinition { Id = 1, Name = "Silver", SortOrder = 2 };
        _db.RankDefinitions.Add(rankDef);
        _db.MemberRankHistories.Add(new MemberRankHistory
        {
            MemberId         = MemberId,
            RankDefinitionId = rankDef.Id,
            AchievedAt       = DateTime.UtcNow.AddDays(-30)
        });
        await _db.SaveChangesAsync();

        var result = await CreateHandler().Handle(new GetRankDashboardQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CurrentRankName.Should().Be("Silver");
    }

    [Fact]
    public async Task Handle_WhenCacheMiss_StoresResultInCache()
    {
        var result = await CreateHandler().Handle(new GetRankDashboardQuery(), default);

        result.IsSuccess.Should().BeTrue();
        _cache.Verify(x => x.SetAsync(
            CacheKeys.MemberRank(MemberId),
            It.IsAny<RankDashboardDto>(),
            CacheKeys.MemberRankTtl,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenMemberHasMemberStats_MapsStatsToDto()
    {
        _db.MemberStatistics.Add(new MemberStatisticEntity
        {
            MemberId                  = MemberId,
            DualTeamPoints            = 1500,
            EnrollmentPoints          = 800,
            QualifiedSponsoredMembers = 5,
            EnrollmentTeamSize        = 12
        });
        await _db.SaveChangesAsync();

        var result = await CreateHandler().Handle(new GetRankDashboardQuery(), default);

        result.Value!.DualTeamPoints.Should().Be(1500);
        result.Value!.EnrollmentPoints.Should().Be(800);
        result.Value!.QualifiedSponsoredMembers.Should().Be(5);
        result.Value!.EnrollmentTeamSize.Should().Be(12);
    }


    [Fact]
    public async Task Handle_WhenNoStatsOrRankHistory_ReturnsEmptyDtoWithMemberId()
    {
        var result = await CreateHandler().Handle(new GetRankDashboardQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MemberId.Should().Be(MemberId);
        result.Value!.CurrentRankName.Should().BeNull();
        result.Value!.DualTeamPoints.Should().Be(0);
        result.Value!.EnrollmentPoints.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenOtherMemberHasHigherRank_DoesNotReturnTheirRank()
    {
        var rankDef = new RankDefinition { Id = 1, Name = "Platinum", SortOrder = 5 };
        _db.RankDefinitions.Add(rankDef);
        _db.MemberRankHistories.Add(new MemberRankHistory
        {
            MemberId         = "other-member",
            RankDefinitionId = rankDef.Id,
            AchievedAt       = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await CreateHandler().Handle(new GetRankDashboardQuery(), default);

        result.Value!.CurrentRankName.Should().BeNull();
    }


    [Fact]
    public async Task Handle_WhenMultipleRanks_LifetimeRankIsHighestSortOrder()
    {
        var silver   = new RankDefinition { Id = 1, Name = "Silver",   SortOrder = 2 };
        var gold     = new RankDefinition { Id = 2, Name = "Gold",     SortOrder = 3 };
        var platinum = new RankDefinition { Id = 3, Name = "Platinum", SortOrder = 5 };
        _db.RankDefinitions.AddRange(silver, gold, platinum);

        _db.MemberRankHistories.AddRange(
            new MemberRankHistory { MemberId = MemberId, RankDefinitionId = silver.Id,   AchievedAt = DateTime.UtcNow.AddDays(-90) },
            new MemberRankHistory { MemberId = MemberId, RankDefinitionId = platinum.Id, AchievedAt = DateTime.UtcNow.AddDays(-60) },
            new MemberRankHistory { MemberId = MemberId, RankDefinitionId = gold.Id,     AchievedAt = DateTime.UtcNow.AddDays(-5) }
        );
        await _db.SaveChangesAsync();

        var result = await CreateHandler().Handle(new GetRankDashboardQuery(), default);

        result.Value!.CurrentRankName.Should().Be("Gold");     // most recent
        result.Value!.LifetimeRankName.Should().Be("Platinum"); // highest sort order
    }
}
