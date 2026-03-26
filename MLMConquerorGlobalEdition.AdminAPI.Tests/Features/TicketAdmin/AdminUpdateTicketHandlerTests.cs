using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminUpdateTicket;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.TicketAdmin;

public class AdminUpdateTicketHandlerTests
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
        Subject = "Help needed",
        Body = "Issue description",
        Status = TicketStatus.Open,
        Priority = TicketPriority.Normal,
        CreationDate = FixedNow.AddDays(-3),
        LastUpdateDate = FixedNow.AddDays(-3),
        CreatedBy = "seed"
    };

    [Fact]
    public async Task Handle_WhenTicketNotFound_ReturnsTicketNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new AdminUpdateTicketHandler(db, CurrentUser().Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new AdminUpdateTicketCommand("nonexistent-id", new AdminUpdateTicketRequest()),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TICKET_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenStatusProvided_UpdatesStatus()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedTicketCategory(db);
        var ticket = BuildTicket();
        await db.Tickets.AddAsync(ticket);
        await db.SaveChangesAsync();

        var handler = new AdminUpdateTicketHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        var result = await handler.Handle(
            new AdminUpdateTicketCommand(ticket.Id, new AdminUpdateTicketRequest
            {
                Status = TicketStatus.InProgress
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("InProgress");
        db.Tickets.Single().Status.Should().Be(TicketStatus.InProgress);
    }

    [Fact]
    public async Task Handle_WhenPriorityProvided_UpdatesPriority()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedTicketCategory(db);
        var ticket = BuildTicket();
        await db.Tickets.AddAsync(ticket);
        await db.SaveChangesAsync();

        var handler = new AdminUpdateTicketHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        var result = await handler.Handle(
            new AdminUpdateTicketCommand(ticket.Id, new AdminUpdateTicketRequest
            {
                Priority = TicketPriority.High
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Priority.Should().Be("High");
        db.Tickets.Single().Priority.Should().Be(TicketPriority.High);
    }

    [Fact]
    public async Task Handle_WhenNeitherStatusNorPriorityProvided_DoesNotChangeValues()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedTicketCategory(db);
        var ticket = BuildTicket();
        await db.Tickets.AddAsync(ticket);
        await db.SaveChangesAsync();

        var handler = new AdminUpdateTicketHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        var result = await handler.Handle(
            new AdminUpdateTicketCommand(ticket.Id, new AdminUpdateTicketRequest()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        db.Tickets.Single().Status.Should().Be(TicketStatus.Open);
        db.Tickets.Single().Priority.Should().Be(TicketPriority.Normal);
    }
}
