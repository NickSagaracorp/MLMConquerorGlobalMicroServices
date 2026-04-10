using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.CreateTicket;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;
using MLMConquerorGlobalEdition.TicketManagementSystem.Tests.Helpers;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Tests.Features.CreateTicket;

public class CreateTicketHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Mock<ICurrentUserService> MemberUser(string memberId = "AMB-001", string userId = "user-001")
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns(userId);
        m.Setup(u => u.MemberId).Returns(memberId);
        m.Setup(u => u.IsAdmin).Returns(false);
        m.Setup(u => u.Roles).Returns(Array.Empty<string>());
        return m;
    }

    private static Mock<IDateTimeProvider> Clock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static Mock<IRoutingEngine> NoRouting()
    {
        var m = new Mock<IRoutingEngine>();
        m.Setup(r => r.RouteAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoutingResult(null, null, false, null));
        return m;
    }

    private static Mock<ISlaMonitorService> NoOpSla() => new Mock<ISlaMonitorService>();

    [Fact]
    public async Task Create_WhenCalled_CreatesTicketAndReturnsDto()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.TicketCategories.AddAsync(new TicketCategory
        {
            Id = 1, Name = "General", CreationDate = FixedNow, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        var handler = new CreateTicketHandler(db, MemberUser().Object, Clock().Object, NoRouting().Object, NoOpSla().Object);

        var result = await handler.Handle(new CreateTicketCommand(new CreateTicketRequest
        {
            Subject = "Test Issue",
            Body = "Something is broken",
            CategoryId = 1,
            Priority = TicketPriority.Normal,
            Channel = TicketChannel.Portal
        }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Subject.Should().Be("Test Issue");
        result.Value.MemberId.Should().Be("AMB-001");
        result.Value.Status.Should().Be("Open");
        db.Tickets.Should().HaveCount(1);
    }

    [Fact]
    public async Task Create_WhenCalled_GeneratesTicketNumberInHDFormat()
    {
        await using var db = InMemoryDbHelper.Create();

        var handler = new CreateTicketHandler(db, MemberUser().Object, Clock().Object, NoRouting().Object, NoOpSla().Object);

        var result = await handler.Handle(new CreateTicketCommand(new CreateTicketRequest
        {
            Subject = "Issue", Body = "Body", CategoryId = 1
        }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TicketNumber.Should().Be("HD-20260401-0001");
    }

    [Fact]
    public async Task Create_WhenMultipleTicketsCreatedSameDay_IncrementsSequence()
    {
        await using var db = InMemoryDbHelper.Create();

        var handler = new CreateTicketHandler(db, MemberUser().Object, Clock().Object, NoRouting().Object, NoOpSla().Object);

        var r1 = await handler.Handle(new CreateTicketCommand(new CreateTicketRequest
        {
            Subject = "T1", Body = "B1", CategoryId = 1
        }), CancellationToken.None);

        var r2 = await handler.Handle(new CreateTicketCommand(new CreateTicketRequest
        {
            Subject = "T2", Body = "B2", CategoryId = 1
        }), CancellationToken.None);

        r1.Value!.TicketNumber.Should().Be("HD-20260401-0001");
        r2.Value!.TicketNumber.Should().Be("HD-20260401-0002");
    }

    [Fact]
    public async Task Create_WhenRoutingAssignsAgent_SetsStatusToInProgress()
    {
        await using var db = InMemoryDbHelper.Create();

        var routing = new Mock<IRoutingEngine>();
        routing.Setup(r => r.RouteAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoutingResult("AGENT-001", 1, false, null));

        var handler = new CreateTicketHandler(db, MemberUser().Object, Clock().Object, routing.Object, NoOpSla().Object);

        var result = await handler.Handle(new CreateTicketCommand(new CreateTicketRequest
        {
            Subject = "Urgent", Body = "Help!", CategoryId = 1
        }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("InProgress");
        result.Value.AssignedToUserId.Should().Be("AGENT-001");
    }
}
