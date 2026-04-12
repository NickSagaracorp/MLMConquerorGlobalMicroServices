using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.ResolveTicket;
using MLMConquerorGlobalEdition.TicketManagementSystem.Tests.Helpers;
using ICurrentUserService  = MLMConquerorGlobalEdition.TicketManagementSystem.Services.ICurrentUserService;
using IDateTimeProvider     = MLMConquerorGlobalEdition.TicketManagementSystem.Services.IDateTimeProvider;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Tests.Features.ResolveTicket;

public class ResolveTicketHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Mock<ICurrentUserService> MemberUser(string memberId = "AMB-001")
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("user-001");
        m.Setup(u => u.MemberId).Returns(memberId);
        m.Setup(u => u.IsAdmin).Returns(false);
        return m;
    }

    private static Mock<ICurrentUserService> AdminUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("admin-001");
        m.Setup(u => u.MemberId).Returns("admin-member");
        m.Setup(u => u.IsAdmin).Returns(true);
        return m;
    }

    private static Mock<IDateTimeProvider> Clock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static Mock<IPushNotificationService> NullPush()
    {
        var m = new Mock<IPushNotificationService>();
        m.Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return m;
    }

    private static Ticket BuildTicket(string id = "TKT-001", string memberId = "AMB-001") => new()
    {
        Id = id,
        TicketNumber = "HD-20260401-0001",
        MemberId = memberId,
        Subject = "Test",
        Body = "Body",
        Status = TicketStatus.InProgress,
        CategoryId = 1,
        Priority = TicketPriority.Normal,
        Channel = TicketChannel.Portal,
        CreationDate = FixedNow,
        CreatedBy = "seed",
        LastUpdateDate = FixedNow
    };

    [Fact]
    public async Task Resolve_WhenTicketNotFound_ReturnsTicketNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new ResolveTicketHandler(db, MemberUser().Object, Clock().Object, NullPush().Object);

        var result = await handler.Handle(
            new ResolveTicketCommand("TKT-GHOST", new ResolveTicketRequest()),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TICKET_NOT_FOUND");
    }

    [Fact]
    public async Task Resolve_WhenNonAdminAccessesOtherMemberTicket_ReturnsForbiddenFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket("TKT-001", "AMB-OTHER"));
        await db.SaveChangesAsync();

        var handler = new ResolveTicketHandler(db, MemberUser("AMB-001").Object, Clock().Object, NullPush().Object);

        var result = await handler.Handle(
            new ResolveTicketCommand("TKT-001", new ResolveTicketRequest()),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Resolve_WhenCalled_SetsStatusToResolvedWithTimestamp()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket("TKT-001", "AMB-001"));
        await db.SaveChangesAsync();

        var handler = new ResolveTicketHandler(db, MemberUser("AMB-001").Object, Clock().Object, NullPush().Object);

        var result = await handler.Handle(
            new ResolveTicketCommand("TKT-001", new ResolveTicketRequest()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var ticket = db.Tickets.Single();
        ticket.Status.Should().Be(TicketStatus.Resolved);
        ticket.ResolvedAt.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Resolve_WhenNotesProvided_AddsResolutionComment()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket("TKT-001", "AMB-001"));
        await db.SaveChangesAsync();

        var handler = new ResolveTicketHandler(db, AdminUser().Object, Clock().Object, NullPush().Object);

        await handler.Handle(
            new ResolveTicketCommand("TKT-001", new ResolveTicketRequest
            {
                ResolutionNotes = "Issue resolved by clearing cache."
            }),
            CancellationToken.None);

        db.TicketComments.Should().HaveCount(1);
        db.TicketComments.Single().Body.Should().Be("Issue resolved by clearing cache.");
    }

    [Fact]
    public async Task Resolve_WhenNoNotes_DoesNotAddComment()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket("TKT-001", "AMB-001"));
        await db.SaveChangesAsync();

        var handler = new ResolveTicketHandler(db, MemberUser("AMB-001").Object, Clock().Object, NullPush().Object);

        await handler.Handle(
            new ResolveTicketCommand("TKT-001", new ResolveTicketRequest { ResolutionNotes = null }),
            CancellationToken.None);

        db.TicketComments.Should().BeEmpty();
    }

    [Fact]
    public async Task Resolve_WhenAdminResolvesAnyTicket_Succeeds()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket("TKT-001", "AMB-OTHER"));
        await db.SaveChangesAsync();

        var handler = new ResolveTicketHandler(db, AdminUser().Object, Clock().Object, NullPush().Object);

        var result = await handler.Handle(
            new ResolveTicketCommand("TKT-001", new ResolveTicketRequest()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
