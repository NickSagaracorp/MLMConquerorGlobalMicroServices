using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.AssignTicket;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;
using MLMConquerorGlobalEdition.TicketManagementSystem.Tests.Helpers;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Tests.Features.AssignTicket;

public class AssignTicketHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Mock<ICurrentUserService> AdminUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("admin-001");
        m.Setup(u => u.IsAdmin).Returns(true);
        return m;
    }

    private static Mock<ICurrentUserService> MemberUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("user-001");
        m.Setup(u => u.IsAdmin).Returns(false);
        return m;
    }

    private static Mock<IDateTimeProvider> Clock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static Ticket BuildTicket(string id = "TKT-001", TicketStatus status = TicketStatus.Open) => new()
    {
        Id = id,
        TicketNumber = "HD-20260401-0001",
        MemberId = "AMB-001",
        Subject = "Test",
        Body = "Body",
        Status = status,
        CategoryId = 1,
        Priority = TicketPriority.Normal,
        Channel = TicketChannel.Portal,
        CreationDate = FixedNow,
        CreatedBy = "seed",
        LastUpdateDate = FixedNow
    };

    [Fact]
    public async Task Assign_WhenNotAdmin_ReturnsForbiddenFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new AssignTicketHandler(db, MemberUser().Object, Clock().Object);

        var result = await handler.Handle(
            new AssignTicketCommand("TKT-001", new AssignTicketRequest { AssignedToUserId = "AGENT-001" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Assign_WhenTicketNotFound_ReturnsTicketNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new AssignTicketHandler(db, AdminUser().Object, Clock().Object);

        var result = await handler.Handle(
            new AssignTicketCommand("TKT-GHOST", new AssignTicketRequest { AssignedToUserId = "AGENT-001" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TICKET_NOT_FOUND");
    }

    [Fact]
    public async Task Assign_WhenOpenTicket_SetsInProgressAndAssignsAgent()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket("TKT-001", TicketStatus.Open));
        await db.SaveChangesAsync();

        var handler = new AssignTicketHandler(db, AdminUser().Object, Clock().Object);

        var result = await handler.Handle(
            new AssignTicketCommand("TKT-001", new AssignTicketRequest { AssignedToUserId = "AGENT-001" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var ticket = db.Tickets.Single();
        ticket.AssignedToUserId.Should().Be("AGENT-001");
        ticket.Status.Should().Be(TicketStatus.InProgress);
    }

    [Fact]
    public async Task Assign_WhenAlreadyInProgress_DoesNotChangeStatus()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket("TKT-001", TicketStatus.InProgress));
        await db.SaveChangesAsync();

        var handler = new AssignTicketHandler(db, AdminUser().Object, Clock().Object);

        await handler.Handle(
            new AssignTicketCommand("TKT-001", new AssignTicketRequest { AssignedToUserId = "AGENT-002" }),
            CancellationToken.None);

        db.Tickets.Single().Status.Should().Be(TicketStatus.InProgress);
        db.Tickets.Single().AssignedToUserId.Should().Be("AGENT-002");
    }
}
