using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.AddComment;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;
using MLMConquerorGlobalEdition.TicketManagementSystem.Tests.Helpers;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Tests.Features.AddComment;

public class AddCommentHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Mock<ICurrentUserService> MemberUser(string memberId = "AMB-001")
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("user-001");
        m.Setup(u => u.MemberId).Returns(memberId);
        m.Setup(u => u.IsAdmin).Returns(false);
        m.Setup(u => u.Roles).Returns(Array.Empty<string>());
        return m;
    }

    private static Mock<ICurrentUserService> AgentUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("agent-001");
        m.Setup(u => u.MemberId).Returns("AMB-001");
        m.Setup(u => u.IsAdmin).Returns(true);
        m.Setup(u => u.Roles).Returns(new[] { "Agent" });
        return m;
    }

    private static Mock<IDateTimeProvider> Clock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static Mock<ISlaMonitorService> NoOpSla() => new Mock<ISlaMonitorService>();

    private static Ticket BuildTicket(
        string id = "TKT-001",
        string memberId = "AMB-001",
        TicketStatus status = TicketStatus.Open) => new()
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
    public async Task AddComment_WhenTicketNotFound_ReturnsTicketNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new AddCommentHandler(db, MemberUser().Object, Clock().Object, NoOpSla().Object);

        var result = await handler.Handle(
            new AddCommentCommand("TKT-GHOST", new AddCommentRequest { Content = "Hello" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TICKET_NOT_FOUND");
    }

    [Fact]
    public async Task AddComment_WhenNonAdminAccessesOtherMemberTicket_ReturnsForbidden()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket("TKT-001", "AMB-OTHER"));
        await db.SaveChangesAsync();

        var handler = new AddCommentHandler(db, MemberUser("AMB-001").Object, Clock().Object, NoOpSla().Object);

        var result = await handler.Handle(
            new AddCommentCommand("TKT-001", new AddCommentRequest { Content = "Hello" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task AddComment_WhenMemberComments_AddsComment()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket("TKT-001", "AMB-001", TicketStatus.Open));
        await db.SaveChangesAsync();

        var handler = new AddCommentHandler(db, MemberUser("AMB-001").Object, Clock().Object, NoOpSla().Object);

        var result = await handler.Handle(
            new AddCommentCommand("TKT-001", new AddCommentRequest { Content = "My comment" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Content.Should().Be("My comment");
        result.Value.IsInternal.Should().BeFalse();
        db.TicketComments.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddComment_WhenAgentCommentsOnInProgress_ChangesStatusToWaitingForUser()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket("TKT-001", "AMB-001", TicketStatus.InProgress));
        await db.SaveChangesAsync();

        var handler = new AddCommentHandler(db, AgentUser().Object, Clock().Object, NoOpSla().Object);

        await handler.Handle(
            new AddCommentCommand("TKT-001", new AddCommentRequest { Content = "Agent reply" }),
            CancellationToken.None);

        db.Tickets.Single().Status.Should().Be(TicketStatus.WaitingForUser);
    }

    [Fact]
    public async Task AddComment_WhenCustomerRepliesWhileWaiting_ChangesStatusToInProgress()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket("TKT-001", "AMB-001", TicketStatus.WaitingForUser));
        await db.SaveChangesAsync();

        var handler = new AddCommentHandler(db, MemberUser("AMB-001").Object, Clock().Object, NoOpSla().Object);

        await handler.Handle(
            new AddCommentCommand("TKT-001", new AddCommentRequest { Content = "Customer reply" }),
            CancellationToken.None);

        db.Tickets.Single().Status.Should().Be(TicketStatus.InProgress);
    }

    [Fact]
    public async Task AddComment_WhenAdminSetsInternal_IsInternalTrue()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddAsync(BuildTicket("TKT-001", "AMB-001", TicketStatus.InProgress));
        await db.SaveChangesAsync();

        var handler = new AddCommentHandler(db, AgentUser().Object, Clock().Object, NoOpSla().Object);

        var result = await handler.Handle(
            new AddCommentCommand("TKT-001", new AddCommentRequest { Content = "Internal note", IsInternal = true }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsInternal.Should().BeTrue();
    }
}
