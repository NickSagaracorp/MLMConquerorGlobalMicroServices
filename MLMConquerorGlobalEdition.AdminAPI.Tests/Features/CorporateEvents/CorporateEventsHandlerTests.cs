using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporateEvents;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporateEvents.CreateCorporateEvent;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporateEvents.DeleteCorporateEvent;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporateEvents.GetCorporateEvents;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporateEvents.UpdateCorporateEvent;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.CorporateEvents;

public class CorporateEventsHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Mock<ICurrentUserService> CurrentUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("admin-001");
        return m;
    }

    private static Mock<IDateTimeProvider> Clock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static PagedRequest Page(int page = 1, int size = 10) => new() { Page = page, PageSize = size };

    [Fact]
    public async Task Create_WhenCalled_CreatesCorporateEventAndReturnsDto()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new CreateCorporateEventHandler(db, CurrentUser().Object, Clock().Object);

        var result = await handler.Handle(new CreateCorporateEventCommand(new CreateCorporateEventRequest
        {
            Title = "Annual Conference",
            Description = "Yearly gathering",
            EventDate = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            Location = "Miami, FL"
        }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("Annual Conference");
        result.Value.IsActive.Should().BeTrue();
        db.CorporateEvents.Should().HaveCount(1);
    }

    [Fact]
    public async Task Create_WhenCreated_SetsCreatedByFromCurrentUser()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new CreateCorporateEventHandler(db, CurrentUser().Object, Clock().Object);

        await handler.Handle(new CreateCorporateEventCommand(new CreateCorporateEventRequest
        {
            Title = "Event",
            EventDate = FixedNow
        }), CancellationToken.None);

        db.CorporateEvents.Single().CreatedBy.Should().Be("admin-001");
    }

    [Fact]
    public async Task Delete_WhenEventNotFound_ReturnsEventNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new DeleteCorporateEventHandler(db, CurrentUser().Object, Clock().Object);

        var result = await handler.Handle(
            new DeleteCorporateEventCommand("EVT-GHOST"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("EVENT_NOT_FOUND");
    }

    [Fact]
    public async Task Delete_WhenEventExists_DeactivatesEvent()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CorporateEvents.AddAsync(new CorporateEvent
        {
            Id = "EVT-001", Title = "Conference", IsActive = true,
            EventDate = FixedNow, CreationDate = FixedNow, CreatedBy = "seed", LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var handler = new DeleteCorporateEventHandler(db, CurrentUser().Object, Clock().Object);

        var result = await handler.Handle(
            new DeleteCorporateEventCommand("EVT-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        db.CorporateEvents.Single().IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Get_WhenNoEvents_ReturnsEmptyPagedResult()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new GetCorporateEventsHandler(db);

        var result = await handler.Handle(
            new GetCorporateEventsQuery(Page()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Get_WhenEventsExist_ReturnsMappedDtos()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CorporateEvents.AddAsync(new CorporateEvent
        {
            Id = "EVT-001", Title = "Summit 2026", IsActive = true,
            EventDate = FixedNow, Location = "NYC",
            CreationDate = FixedNow, CreatedBy = "seed", LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var handler = new GetCorporateEventsHandler(db);

        var result = await handler.Handle(
            new GetCorporateEventsQuery(Page()), CancellationToken.None);

        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Single().Title.Should().Be("Summit 2026");
        result.Value.Items.Single().Location.Should().Be("NYC");
    }

    [Fact]
    public async Task Get_ReturnsPaginatedSubset()
    {
        await using var db = InMemoryDbHelper.Create();
        for (int i = 0; i < 5; i++)
        {
            await db.CorporateEvents.AddAsync(new CorporateEvent
            {
                Title = $"Event {i}", IsActive = true,
                EventDate = FixedNow.AddDays(i),
                CreationDate = FixedNow, CreatedBy = "seed", LastUpdateDate = FixedNow
            });
        }
        await db.SaveChangesAsync();

        var handler = new GetCorporateEventsHandler(db);

        var result = await handler.Handle(
            new GetCorporateEventsQuery(Page(page: 1, size: 3)), CancellationToken.None);

        result.Value!.TotalCount.Should().Be(5);
        result.Value.Items.Count().Should().Be(3);
    }

    [Fact]
    public async Task Update_WhenEventNotFound_ReturnsEventNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UpdateCorporateEventHandler(db, CurrentUser().Object, Clock().Object);

        var result = await handler.Handle(
            new UpdateCorporateEventCommand("EVT-GHOST", new UpdateCorporateEventRequest
            {
                Title = "Updated", EventDate = FixedNow, IsActive = true
            }), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("EVENT_NOT_FOUND");
    }

    [Fact]
    public async Task Update_WhenEventExists_UpdatesAllFields()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CorporateEvents.AddAsync(new CorporateEvent
        {
            Id = "EVT-001", Title = "Old Title", IsActive = true,
            EventDate = FixedNow, Location = "Old Location",
            CreationDate = FixedNow, CreatedBy = "seed", LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var handler = new UpdateCorporateEventHandler(db, CurrentUser().Object, Clock().Object);

        var result = await handler.Handle(
            new UpdateCorporateEventCommand("EVT-001", new UpdateCorporateEventRequest
            {
                Title = "New Title",
                EventDate = FixedNow.AddMonths(1),
                Location = "New Location",
                IsActive = false
            }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("New Title");
        result.Value.IsActive.Should().BeFalse();

        var updated = db.CorporateEvents.Single();
        updated.Location.Should().Be("New Location");
        updated.LastUpdateBy.Should().Be("admin-001");
    }
}
