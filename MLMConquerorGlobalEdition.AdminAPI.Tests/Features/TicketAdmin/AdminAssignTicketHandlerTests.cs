using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.AdminAssignTicket;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.TicketAdmin;

public class AdminAssignTicketHandlerTests
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
        Subject = "Login issue",
        Body = "Cannot log in",
        Status = TicketStatus.Open,
        Priority = TicketPriority.Normal,
        CreationDate = FixedNow.AddDays(-2),
        LastUpdateDate = FixedNow.AddDays(-2),
        CreatedBy = "seed"
    };

    [Fact]
    public async Task Handle_WhenTicketNotFound_ReturnsTicketNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new AdminAssignTicketHandler(db, CurrentUser().Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new AdminAssignTicketCommand("nonexistent-id", new AdminAssignTicketRequest
            {
                AssignedToUserId = "support-user-001"
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TICKET_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenTicketFound_AssignsUserAndSetsStatusToInProgress()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedTicketCategory(db);
        var ticket = BuildTicket();
        await db.Tickets.AddAsync(ticket);
        await db.SaveChangesAsync();

        var handler = new AdminAssignTicketHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        var result = await handler.Handle(
            new AdminAssignTicketCommand(ticket.Id, new AdminAssignTicketRequest
            {
                AssignedToUserId = "support-user-001"
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("InProgress");
        result.Value.AssignedToUserId.Should().Be("support-user-001");

        var updated = db.Tickets.Single();
        updated.AssignedToUserId.Should().Be("support-user-001");
        updated.Status.Should().Be(TicketStatus.InProgress);
    }

    [Fact]
    public async Task Handle_WhenAssigned_ReturnsTicketDtoWithCorrectData()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedTicketCategory(db);
        var ticket = BuildTicket();
        await db.Tickets.AddAsync(ticket);
        await db.SaveChangesAsync();

        var handler = new AdminAssignTicketHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        var result = await handler.Handle(
            new AdminAssignTicketCommand(ticket.Id, new AdminAssignTicketRequest
            {
                AssignedToUserId = "agent-007"
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MemberId.Should().Be("AMB-001");
        result.Value.Subject.Should().Be("Login issue");
    }
}
