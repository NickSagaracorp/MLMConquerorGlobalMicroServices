using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Ranks;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens;
using MLMConquerorGlobalEdition.BizCenter.Features.Loyalty.GetLoyaltyPoints;
using MLMConquerorGlobalEdition.BizCenter.Features.Ranks.GetRankHistory;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetGuestPasses;
using MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetTokenBalances;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Loyalty;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using ICurrentUserService = MLMConquerorGlobalEdition.BizCenter.Services.ICurrentUserService;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

public class TokenLoyaltyRankHandlersTests : IDisposable
{
    private const string MemberId = "member-tok-001";

    private readonly AppDbContext _db;
    private readonly Mock<ICurrentUserService> _currentUser;
    private readonly Mock<ICacheService> _cache;

    public TokenLoyaltyRankHandlersTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _currentUser = new Mock<ICurrentUserService>();
        _currentUser.Setup(x => x.MemberId).Returns(MemberId);

        _cache = new Mock<ICacheService>();
        _cache.Setup(x => x.GetAsync<List<TokenBalanceDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((List<TokenBalanceDto>?)null);
        _cache.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<List<TokenBalanceDto>>(),
                                      It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);
    }

    public void Dispose() => _db.Dispose();


    [Fact]
    public async Task GetTokenBalances_WhenMemberHasMultipleTokenTypes_ReturnsBothBalances()
    {
        _db.TokenTypes.AddRange(
            new TokenType { Id = 1, Name = "Regular Token", IsGuestPass = false },
            new TokenType { Id = 2, Name = "Guest Pass",    IsGuestPass = true  }
        );
        _db.TokenBalances.AddRange(
            new TokenBalance { MemberId = MemberId, TokenTypeId = 1, Balance = 10 },
            new TokenBalance { MemberId = MemberId, TokenTypeId = 2, Balance = 5  }
        );
        await _db.SaveChangesAsync();

        var handler = new GetTokenBalancesHandler(_db, _currentUser.Object, _cache.Object);
        var result  = await handler.Handle(new GetTokenBalancesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
        result.Value.Sum(b => b.Balance).Should().Be(15);
    }

    [Fact]
    public async Task GetTokenBalances_WhenCacheHit_ReturnsCachedData()
    {
        var cached = new List<TokenBalanceDto>
        {
            new() { TokenTypeName = "Cached Token", IsGuestPass = false, Balance = 99 }
        };
        _cache.Setup(x => x.GetAsync<List<TokenBalanceDto>>(
                CacheKeys.MemberTokenBalances(MemberId), It.IsAny<CancellationToken>()))
              .ReturnsAsync(cached);

        var handler = new GetTokenBalancesHandler(_db, _currentUser.Object, _cache.Object);
        var result  = await handler.Handle(new GetTokenBalancesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.First().Balance.Should().Be(99);
        _cache.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<List<TokenBalanceDto>>(),
                                       It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
                      Times.Never);
    }

    [Fact]
    public async Task GetTokenBalances_WhenCacheMiss_StoresResultInCache()
    {
        _db.TokenTypes.Add(new TokenType { Id = 1, Name = "Token", IsGuestPass = false });
        _db.TokenBalances.Add(new TokenBalance { MemberId = MemberId, TokenTypeId = 1, Balance = 3 });
        await _db.SaveChangesAsync();

        var handler = new GetTokenBalancesHandler(_db, _currentUser.Object, _cache.Object);
        await handler.Handle(new GetTokenBalancesQuery(), default);

        _cache.Verify(x => x.SetAsync(
            CacheKeys.MemberTokenBalances(MemberId),
            It.IsAny<List<TokenBalanceDto>>(),
            CacheKeys.MemberTokenBalancesTtl,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTokenBalances_WhenOtherMemberHasBalances_DoesNotReturnThem()
    {
        _db.TokenTypes.Add(new TokenType { Id = 1, Name = "Token", IsGuestPass = false });
        _db.TokenBalances.Add(new TokenBalance { MemberId = "other-member", TokenTypeId = 1, Balance = 50 });
        await _db.SaveChangesAsync();

        var handler = new GetTokenBalancesHandler(_db, _currentUser.Object, _cache.Object);
        var result  = await handler.Handle(new GetTokenBalancesQuery(), default);

        result.Value!.Should().BeEmpty();
    }


    [Fact]
    public async Task GetGuestPasses_WhenMemberHasGuestPassBalance_ReturnsOnlyGuestPasses()
    {
        _db.TokenTypes.AddRange(
            new TokenType { Id = 1, Name = "Regular Token", IsGuestPass = false },
            new TokenType { Id = 2, Name = "Guest Pass",    IsGuestPass = true  }
        );
        _db.TokenBalances.AddRange(
            new TokenBalance { MemberId = MemberId, TokenTypeId = 1, Balance = 10 },
            new TokenBalance { MemberId = MemberId, TokenTypeId = 2, Balance = 4  }
        );
        await _db.SaveChangesAsync();

        var handler = new GetGuestPassesHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetGuestPassesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
        result.Value.First().IsGuestPass.Should().BeTrue();
        result.Value.First().Balance.Should().Be(4);
    }

    [Fact]
    public async Task GetGuestPasses_WhenNoGuestPassBalance_ReturnsEmpty()
    {
        _db.TokenTypes.Add(new TokenType { Id = 1, Name = "Regular Token", IsGuestPass = false });
        _db.TokenBalances.Add(new TokenBalance { MemberId = MemberId, TokenTypeId = 1, Balance = 10 });
        await _db.SaveChangesAsync();

        var handler = new GetGuestPassesHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetGuestPassesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGuestPasses_WhenOtherMemberHasGuestPass_DoesNotReturnIt()
    {
        _db.TokenTypes.Add(new TokenType { Id = 2, Name = "Guest Pass", IsGuestPass = true });
        _db.TokenBalances.Add(new TokenBalance { MemberId = "other-member", TokenTypeId = 2, Balance = 3 });
        await _db.SaveChangesAsync();

        var handler = new GetGuestPassesHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetGuestPassesQuery(), default);

        result.Value!.Should().BeEmpty();
    }


    [Fact]
    public async Task GetLoyaltyPoints_WhenMemberHasPoints_ReturnsPagedResult()
    {
        _db.LoyaltyPoints.AddRange(
            new LoyaltyPoints { MemberId = MemberId, ProductId = "p1", OrderId = "o1", PointsEarned = 50, MonthNo = 1, YearNo = 2026, CreationDate = DateTime.UtcNow, CreatedBy = "seed", LastUpdateDate = DateTime.UtcNow },
            new LoyaltyPoints { MemberId = MemberId, ProductId = "p2", OrderId = "o2", PointsEarned = 30, MonthNo = 2, YearNo = 2026, CreationDate = DateTime.UtcNow, CreatedBy = "seed", LastUpdateDate = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        var handler = new GetLoyaltyPointsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetLoyaltyPointsQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Sum(lp => lp.PointsEarned).Should().Be(80m);
    }

    [Fact]
    public async Task GetLoyaltyPoints_WhenNoPoints_ReturnsEmptyPage()
    {
        var handler = new GetLoyaltyPointsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetLoyaltyPointsQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(0);
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLoyaltyPoints_WhenOtherMemberHasPoints_DoesNotReturnThem()
    {
        _db.LoyaltyPoints.Add(
            new LoyaltyPoints { MemberId = "other-member", ProductId = "p1", OrderId = "o1", PointsEarned = 100, MonthNo = 1, YearNo = 2026, CreationDate = DateTime.UtcNow, CreatedBy = "seed", LastUpdateDate = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        var handler = new GetLoyaltyPointsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetLoyaltyPointsQuery(1, 20), default);

        result.Value!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetLoyaltyPoints_WhenItemsExceedPageSize_PaginatesCorrectly()
    {
        for (int i = 0; i < 5; i++)
        {
            _db.LoyaltyPoints.Add(new LoyaltyPoints
            {
                MemberId     = MemberId,
                ProductId    = $"p-{i}",
                OrderId      = $"o-{i}",
                PointsEarned = 10,
                MonthNo      = i + 1,
                YearNo       = 2026,
                CreationDate = DateTime.UtcNow,
                CreatedBy    = "seed",
                LastUpdateDate = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();

        var handler = new GetLoyaltyPointsHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetLoyaltyPointsQuery(1, 3), default);

        result.Value!.TotalCount.Should().Be(5);
        result.Value.Items.Should().HaveCount(3);
    }


    [Fact]
    public async Task GetRankHistory_WhenMemberHasRankHistory_ReturnsPagedHistory()
    {
        var rank = new RankDefinition { Id = 1, Name = "Gold", SortOrder = 3 };
        _db.RankDefinitions.Add(rank);
        _db.MemberRankHistories.AddRange(
            new MemberRankHistory { MemberId = MemberId, RankDefinitionId = rank.Id, AchievedAt = DateTime.UtcNow.AddDays(-60) },
            new MemberRankHistory { MemberId = MemberId, RankDefinitionId = rank.Id, AchievedAt = DateTime.UtcNow.AddDays(-10) }
        );
        await _db.SaveChangesAsync();

        var handler = new GetRankHistoryHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetRankHistoryQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.All(r => r.RankName == "Gold").Should().BeTrue();
    }

    [Fact]
    public async Task GetRankHistory_WhenNoHistory_ReturnsEmptyPage()
    {
        var handler = new GetRankHistoryHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetRankHistoryQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(0);
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRankHistory_WhenOtherMemberHasHistory_DoesNotReturnIt()
    {
        var rank = new RankDefinition { Id = 1, Name = "Silver", SortOrder = 2 };
        _db.RankDefinitions.Add(rank);
        _db.MemberRankHistories.Add(
            new MemberRankHistory { MemberId = "other-member", RankDefinitionId = rank.Id, AchievedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        var handler = new GetRankHistoryHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetRankHistoryQuery(1, 20), default);

        result.Value!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetRankHistory_IsOrderedByAchievedAtDescending()
    {
        var rank = new RankDefinition { Id = 1, Name = "Bronze", SortOrder = 1 };
        _db.RankDefinitions.Add(rank);
        var older = new MemberRankHistory { MemberId = MemberId, RankDefinitionId = rank.Id, AchievedAt = DateTime.UtcNow.AddDays(-90) };
        var newer = new MemberRankHistory { MemberId = MemberId, RankDefinitionId = rank.Id, AchievedAt = DateTime.UtcNow.AddDays(-5) };
        _db.MemberRankHistories.AddRange(older, newer);
        await _db.SaveChangesAsync();

        var handler = new GetRankHistoryHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetRankHistoryQuery(1, 20), default);

        result.Value!.Items.First().AchievedAt
              .Should().BeCloseTo(newer.AchievedAt, precision: TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetRankHistory_WhenItemsExceedPageSize_PaginatesCorrectly()
    {
        var rank = new RankDefinition { Id = 1, Name = "Bronze", SortOrder = 1 };
        _db.RankDefinitions.Add(rank);
        for (int i = 0; i < 5; i++)
            _db.MemberRankHistories.Add(new MemberRankHistory
            {
                MemberId         = MemberId,
                RankDefinitionId = rank.Id,
                AchievedAt       = DateTime.UtcNow.AddDays(-i)
            });
        await _db.SaveChangesAsync();

        var handler = new GetRankHistoryHandler(_db, _currentUser.Object);
        var result  = await handler.Handle(new GetRankHistoryQuery(1, 3), default);

        result.Value!.TotalCount.Should().Be(5);
        result.Value.Items.Should().HaveCount(3);
    }
}
