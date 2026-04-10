using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.EscalateTicket;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;
using MLMConquerorGlobalEdition.TicketManagementSystem.Tests.Helpers;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Tests.Features.EscalateTicket;

public class EscalateTicketHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Mock<ICurrentUserService> AgentUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("agent-001");
        m.Setup(u => u.IsAdmin).Returns(true);
        m.Setup(u => u.Roles).Returns(new[] { "Agent" });
        return m;
    }

    private static Mock<ICurrentUserService> MemberUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("user-001");
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

    private static Ticket BuildTicket(
        string id = "TKT-001",
        TicketStatus status = TicketStatus.InProgress,
        EscalationLevel level = EscalationLevel.Tier1) => new()
    {
        Id = id,
        TicketNumber = "HD-20260401-0001",
        MemberId = "AMB-001",
        Subject = "Test",
        Body = "Body",
        Status = status,
        EscalationLevel = level,
        CategoryId = 1,
        Priority = TicketPriority.Normal,
        Channel = TicketChannel.Portal,
        CreationDate = FixedNow,
        CreatedBy = "seed",
        LastUpdateDate = FixedNow
    };

    [Fact]
    public async Task Escalate_WhenTicketNotFound_ReturnsTicketNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new EscalateTicketHandler(db, AgentUser().Object, Clock().Object);

        var result = await handler.Handle(
            new EscalateTicketCommand("TKT-GHOST", new EscalateTicketRequest { Reason = "Critical" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TICKET_NOT_FOUND");
    }

    [Fact]
    public async Task Escalate_WhenNotAgentOrAdmin_ReturnsForbiddenFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket());
        await db.SaveChangesAsync();

        var handler = new EscalateTicketHandler(db, MemberUser().Object, Clock().Object);

        var result = await handler.Handle(
            new EscalateTicketCommand("TKT-001", new EscalateTicketRequest { Reason = "Critical" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Escalate_WhenTicketAlreadyResolved_ReturnsAlreadyClosedFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket(status: TicketStatus.Resolved));
        await db.SaveChangesAsync();

        var handler = new EscalateTicketHandler(db, AgentUser().Object, Clock().Object);

        var result = await handler.Handle(
            new EscalateTicketCommand("TKT-001", new EscalateTicketRequest { Reason = "Attempt" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TICKET_ALREADY_CLOSED");
    }

    [Fact]
    public async Task Escalate_WhenCalled_IncrementsEscalationLevelAndAddsComment()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket(level: EscalationLevel.Tier1));
        await db.SaveChangesAsync();

        var handler = new EscalateTicketHandler(db, AgentUser().Object, Clock().Object);

        var result = await handler.Handle(
            new EscalateTicketCommand("TKT-001", new EscalateTicketRequest { Reason = "Customer demand" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var ticket = db.Tickets.Single();
        ticket.EscalationLevel.Should().Be(EscalationLevel.Tier2);
        ticket.Status.Should().Be(TicketStatus.InProgress);
        db.TicketComments.Should().HaveCount(1);
    }

    [Fact]
    public async Task Escalate_WhenAtMaxLevel_StaysAtTier3()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket(level: EscalationLevel.Tier3));
        await db.SaveChangesAsync();

        var handler = new EscalateTicketHandler(db, AgentUser().Object, Clock().Object);

        var result = await handler.Handle(
            new EscalateTicketCommand("TKT-001", new EscalateTicketRequest { Reason = "Max escalation" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        db.Tickets.Single().EscalationLevel.Should().Be(EscalationLevel.Tier3);
    }
}
