using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.CloseTicket;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;
using MLMConquerorGlobalEdition.TicketManagementSystem.Tests.Helpers;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Tests.Features.CloseTicket;

public class CloseTicketHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Mock<ICurrentUserService> AdminUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("admin-001");
        m.Setup(u => u.MemberId).Returns("AMB-ADMIN");
        m.Setup(u => u.IsAdmin).Returns(true);
        return m;
    }

    private static Mock<ICurrentUserService> MemberUser(string memberId = "AMB-001")
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("user-001");
        m.Setup(u => u.MemberId).Returns(memberId);
        m.Setup(u => u.IsAdmin).Returns(false);
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
        string memberId = "AMB-001",
        TicketStatus status = TicketStatus.InProgress) => new()
    {
        Id = id,
        TicketNumber = "HD-20260401-0001",
        MemberId = memberId,
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
    public async Task Close_WhenTicketNotFound_ReturnsTicketNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new CloseTicketHandler(db, AdminUser().Object, Clock().Object);

        var result = await handler.Handle(
            new CloseTicketCommand("TKT-GHOST"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TICKET_NOT_FOUND");
    }

    [Fact]
    public async Task Close_WhenAlreadyClosed_ReturnsAlreadyClosedFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket(status: TicketStatus.Closed));
        await db.SaveChangesAsync();

        var handler = new CloseTicketHandler(db, AdminUser().Object, Clock().Object);

        var result = await handler.Handle(
            new CloseTicketCommand("TKT-001"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TICKET_ALREADY_CLOSED");
    }

    [Fact]
    public async Task Close_WhenNonAdminAccessesOtherMemberTicket_ReturnsForbiddenFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket("TKT-001", "AMB-OTHER", TicketStatus.InProgress));
        await db.SaveChangesAsync();

        var handler = new CloseTicketHandler(db, MemberUser("AMB-001").Object, Clock().Object);

        var result = await handler.Handle(
            new CloseTicketCommand("TKT-001"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Close_WhenCalled_SetsStatusToClosedWithTimestamp()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket("TKT-001", "AMB-001", TicketStatus.Resolved));
        await db.SaveChangesAsync();

        var handler = new CloseTicketHandler(db, MemberUser("AMB-001").Object, Clock().Object);

        var result = await handler.Handle(
            new CloseTicketCommand("TKT-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var ticket = db.Tickets.Single();
        ticket.Status.Should().Be(TicketStatus.Closed);
        ticket.ClosedAt.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Close_WhenAdminCloses_Succeeds()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket("TKT-001", "AMB-OTHER", TicketStatus.Open));
        await db.SaveChangesAsync();

        var handler = new CloseTicketHandler(db, AdminUser().Object, Clock().Object);

        var result = await handler.Handle(
            new CloseTicketCommand("TKT-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        db.Tickets.Single().Status.Should().Be(TicketStatus.Closed);
    }
}
