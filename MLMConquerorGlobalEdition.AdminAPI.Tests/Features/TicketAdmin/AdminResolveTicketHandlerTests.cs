using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminResolveTicket;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.TicketAdmin;

public class AdminResolveTicketHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<ICurrentUserService> CurrentUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("admin-001");
        return m;
    }

    private static Mock<IDateTimeProvider> DateTimeProvider()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static async Task SeedTicketCategory(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db, int id = 1)
    {
        await db.TicketCategories.AddAsync(new TicketCategory
        {
            Id = id,
            Name = "General",
            IsActive = true,
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync();
    }

    private static Ticket BuildTicket(int categoryId = 1) => new()
    {
        MemberId = "AMB-001",
        CategoryId = categoryId,
        Subject = "Billing question",
        Body = "Why was I charged?",
        Status = TicketStatus.InProgress,
        Priority = TicketPriority.Normal,
        CreationDate = FixedNow.AddDays(-4),
        LastUpdateDate = FixedNow.AddDays(-1),
        CreatedBy = "seed"
    };

    [Fact]
    public async Task Handle_WhenTicketNotFound_ReturnsTicketNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new AdminResolveTicketHandler(db, CurrentUser().Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new AdminResolveTicketCommand("nonexistent-id", new AdminResolveTicketRequest()),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TICKET_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenTicketFound_SetsStatusToResolvedAndSetsResolvedAt()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedTicketCategory(db);
        var ticket = BuildTicket();
        await db.Tickets.AddAsync(ticket);
        await db.SaveChangesAsync();

        var handler = new AdminResolveTicketHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        var result = await handler.Handle(
            new AdminResolveTicketCommand(ticket.Id, new AdminResolveTicketRequest()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Resolved");

        var updated = db.Tickets.Single();
        updated.Status.Should().Be(TicketStatus.Resolved);
        updated.ResolvedAt.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Handle_WhenResolutionNotesProvided_CreatesInternalComment()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedTicketCategory(db);
        var ticket = BuildTicket();
        await db.Tickets.AddAsync(ticket);
        await db.SaveChangesAsync();

        var handler = new AdminResolveTicketHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        await handler.Handle(
            new AdminResolveTicketCommand(ticket.Id, new AdminResolveTicketRequest
            {
                ResolutionNotes = "Issue resolved via refund"
            }),
            CancellationToken.None);

        var comment = db.TicketComments.Single();
        comment.TicketId.Should().Be(ticket.Id);
        comment.Body.Should().Be("Issue resolved via refund");
        comment.IsInternal.Should().BeTrue();
        comment.AuthorId.Should().Be("admin-001");
    }

    [Fact]
    public async Task Handle_WhenNoResolutionNotes_DoesNotCreateComment()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedTicketCategory(db);
        var ticket = BuildTicket();
        await db.Tickets.AddAsync(ticket);
        await db.SaveChangesAsync();

        var handler = new AdminResolveTicketHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        await handler.Handle(
            new AdminResolveTicketCommand(ticket.Id, new AdminResolveTicketRequest
            {
                ResolutionNotes = null
            }),
            CancellationToken.None);

        db.TicketComments.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenWhitespaceResolutionNotes_DoesNotCreateComment()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedTicketCategory(db);
        var ticket = BuildTicket();
        await db.Tickets.AddAsync(ticket);
        await db.SaveChangesAsync();

        var handler = new AdminResolveTicketHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        await handler.Handle(
            new AdminResolveTicketCommand(ticket.Id, new AdminResolveTicketRequest
            {
                ResolutionNotes = "   "
            }),
            CancellationToken.None);

        db.TicketComments.Should().BeEmpty();
    }
}
