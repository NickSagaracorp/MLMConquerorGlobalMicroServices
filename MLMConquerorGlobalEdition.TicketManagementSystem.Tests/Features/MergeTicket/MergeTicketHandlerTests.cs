using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.MergeTicket;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;
using MLMConquerorGlobalEdition.TicketManagementSystem.Tests.Helpers;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Tests.Features.MergeTicket;

public class MergeTicketHandlerTests
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

    private static Ticket BuildTicket(string id) => new()
    {
        Id = id,
        TicketNumber = $"HD-20260401-{id}",
        MemberId = "AMB-001",
        Subject = $"Ticket {id}",
        Body = "Body",
        Status = TicketStatus.Open,
        CategoryId = 1,
        Priority = TicketPriority.Normal,
        Channel = TicketChannel.Portal,
        CreationDate = FixedNow,
        CreatedBy = "seed",
        LastUpdateDate = FixedNow
    };

    [Fact]
    public async Task Merge_WhenNotAdmin_ReturnsForbiddenFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new MergeTicketHandler(db, MemberUser().Object, Clock().Object);

        var result = await handler.Handle(
            new MergeTicketCommand("TKT-001", new MergeTicketRequest { TargetTicketId = "TKT-002" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Merge_WhenSameTicket_ReturnsSameTicketFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new MergeTicketHandler(db, AdminUser().Object, Clock().Object);

        var result = await handler.Handle(
            new MergeTicketCommand("TKT-001", new MergeTicketRequest { TargetTicketId = "TKT-001" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MERGE_SAME_TICKET");
    }

    [Fact]
    public async Task Merge_WhenSourceNotFound_ReturnsTicketNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket("TKT-002"));
        await db.SaveChangesAsync();

        var handler = new MergeTicketHandler(db, AdminUser().Object, Clock().Object);

        var result = await handler.Handle(
            new MergeTicketCommand("TKT-GHOST", new MergeTicketRequest { TargetTicketId = "TKT-002" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TICKET_NOT_FOUND");
    }

    [Fact]
    public async Task Merge_WhenTargetNotFound_ReturnsTargetNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket("TKT-001"));
        await db.SaveChangesAsync();

        var handler = new MergeTicketHandler(db, AdminUser().Object, Clock().Object);

        var result = await handler.Handle(
            new MergeTicketCommand("TKT-001", new MergeTicketRequest { TargetTicketId = "TKT-GHOST" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TARGET_TICKET_NOT_FOUND");
    }

    [Fact]
    public async Task Merge_WhenCalled_ClosesSourceAndSetsMergedIntoTarget()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddRangeAsync(
            BuildTicket("TKT-001"),
            BuildTicket("TKT-002")
        );
        await db.SaveChangesAsync();

        var handler = new MergeTicketHandler(db, AdminUser().Object, Clock().Object);

        var result = await handler.Handle(
            new MergeTicketCommand("TKT-001", new MergeTicketRequest { TargetTicketId = "TKT-002" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var source = db.Tickets.Single(t => t.Id == "TKT-001");
        source.Status.Should().Be(TicketStatus.Closed);
        source.MergedIntoTicketId.Should().Be("TKT-002");

        var target = db.Tickets.Single(t => t.Id == "TKT-002");
        target.Status.Should().Be(TicketStatus.Open);
    }
}
