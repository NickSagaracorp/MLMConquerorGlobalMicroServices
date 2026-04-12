using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Queries.GetMembershipLevels;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Signups;

public class GetMembershipLevelsHandlerTests
{
    private static Mock<ICacheService> NullCache()
    {
        var m = new Mock<ICacheService>();
        m.Setup(c => c.GetAsync<List<MembershipLevelDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync((List<MembershipLevelDto>?)null);
        m.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<List<MembershipLevelDto>>(),
                                It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
         .Returns(Task.CompletedTask);
        return m;
    }

    [Fact]
    public async Task Handle_WhenActiveLevelsExist_ReturnsSortedBySortOrder()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddRangeAsync(
            new MembershipLevel { Id = 3, Name = "Elite",      SortOrder = 30, IsActive = true, CreatedBy = "seed", Price = 150 },
            new MembershipLevel { Id = 1, Name = "Ambassador", SortOrder = 10, IsActive = true, CreatedBy = "seed", Price = 50 },
            new MembershipLevel { Id = 2, Name = "VIP",        SortOrder = 20, IsActive = true, CreatedBy = "seed", Price = 80 }
        );
        await db.SaveChangesAsync();

        var handler = new GetMembershipLevelsHandler(db, NullCache().Object);
        var result  = await handler.Handle(new GetMembershipLevelsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var items = result.Value!.ToList();
        items.Should().HaveCount(3);
        items[0].Name.Should().Be("Ambassador");
        items[1].Name.Should().Be("VIP");
        items[2].Name.Should().Be("Elite");
    }

    [Fact]
    public async Task Handle_WhenNoActiveLevels_ReturnsEmptyList()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(
            new MembershipLevel { Id = 1, Name = "Inactive", SortOrder = 10, IsActive = false, CreatedBy = "seed", Price = 50 }
        );
        await db.SaveChangesAsync();

        var handler = new GetMembershipLevelsHandler(db, NullCache().Object);
        var result  = await handler.Handle(new GetMembershipLevelsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_FiltersInactiveLevels_OnlyActiveReturned()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddRangeAsync(
            new MembershipLevel { Id = 1, Name = "Active",   SortOrder = 10, IsActive = true,  CreatedBy = "seed", Price = 50 },
            new MembershipLevel { Id = 2, Name = "Inactive", SortOrder = 20, IsActive = false, CreatedBy = "seed", Price = 80 }
        );
        await db.SaveChangesAsync();

        var handler = new GetMembershipLevelsHandler(db, NullCache().Object);
        var result  = await handler.Handle(new GetMembershipLevelsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
        result.Value.Single().Name.Should().Be("Active");
    }

    [Fact]
    public async Task Handle_WhenCacheHit_ReturnsCachedDataWithoutHittingDb()
    {
        await using var db = InMemoryDbHelper.Create();
        var cached = new List<MembershipLevelDto> { new() { Id = 99, Name = "Cached" } };

        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetAsync<List<MembershipLevelDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(cached);

        var handler = new GetMembershipLevelsHandler(db, cache.Object);
        var result  = await handler.Handle(new GetMembershipLevelsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Single().Name.Should().Be("Cached");
    }
}
