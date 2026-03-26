using AutoMapper;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Signups.DTOs;
using MLMConquerorGlobalEdition.Signups.Features.Signups.Queries.GetMembershipLevels;
using MLMConquerorGlobalEdition.Signups.Tests.Helpers;

namespace MLMConquerorGlobalEdition.Signups.Tests.Features.Signups;

public class GetMembershipLevelsHandlerTests
{
    private static Mock<IMapper> CreateMapper(IEnumerable<MembershipLevel> levels, IEnumerable<MembershipLevelDto> dtos)
    {
        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<IEnumerable<MembershipLevelDto>>(It.IsAny<IEnumerable<MembershipLevel>>()))
              .Returns(dtos);
        return mapper;
    }

    [Fact]
    public async Task Handle_WhenActiveLevelsExist_ReturnsThemOrderedBySortOrder()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddRangeAsync(
            new MembershipLevel { Id = 3, Name = "Elite", SortOrder = 30, IsActive = true, CreatedBy = "seed" },
            new MembershipLevel { Id = 1, Name = "Ambassador", SortOrder = 10, IsActive = true, CreatedBy = "seed" },
            new MembershipLevel { Id = 2, Name = "VIP", SortOrder = 20, IsActive = true, CreatedBy = "seed" }
        );
        await db.SaveChangesAsync();

        var expectedDtos = new List<MembershipLevelDto>
        {
            new() { Id = 1, Name = "Ambassador", SortOrder = 10 },
            new() { Id = 2, Name = "VIP",         SortOrder = 20 },
            new() { Id = 3, Name = "Elite",       SortOrder = 30 }
        };
        var mapper = CreateMapper(It.IsAny<IEnumerable<MembershipLevel>>(), expectedDtos);

        var handler = new GetMembershipLevelsHandler(db, mapper.Object);
        var result = await handler.Handle(new GetMembershipLevelsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WhenNoActiveLevelsExist_ReturnsEmptyList()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(
            new MembershipLevel { Id = 1, Name = "Inactive", SortOrder = 10, IsActive = false, CreatedBy = "seed" }
        );
        await db.SaveChangesAsync();

        var mapper = CreateMapper(Enumerable.Empty<MembershipLevel>(), Enumerable.Empty<MembershipLevelDto>());

        var handler = new GetMembershipLevelsHandler(db, mapper.Object);
        var result = await handler.Handle(new GetMembershipLevelsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_FiltersOutInactiveLevels_ReturnsOnlyActive()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddRangeAsync(
            new MembershipLevel { Id = 1, Name = "Active Level", SortOrder = 10, IsActive = true, CreatedBy = "seed" },
            new MembershipLevel { Id = 2, Name = "Inactive Level", SortOrder = 20, IsActive = false, CreatedBy = "seed" }
        );
        await db.SaveChangesAsync();

        // Capture what levels were passed to the mapper
        IEnumerable<MembershipLevel>? capturedInput = null;
        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<IEnumerable<MembershipLevelDto>>(It.IsAny<IEnumerable<MembershipLevel>>()))
              .Callback<object>(obj => capturedInput = (IEnumerable<MembershipLevel>)obj)
              .Returns(new List<MembershipLevelDto> { new() { Id = 1, Name = "Active Level" } });

        var handler = new GetMembershipLevelsHandler(db, mapper.Object);
        await handler.Handle(new GetMembershipLevelsQuery(), CancellationToken.None);

        capturedInput.Should().NotBeNull();
        capturedInput!.Should().HaveCount(1);
        capturedInput.Single().IsActive.Should().BeTrue();
    }
}
