using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.GetTickets;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;
using MLMConquerorGlobalEdition.TicketManagementSystem.Tests.Helpers;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Tests.Features.GetTickets;

public class GetTicketsHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    private static PagedRequest Page(int page = 1, int size = 10) => new() { Page = page, PageSize = size };

    private static Mock<ICurrentUserService> AdminUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.MemberId).Returns("AMB-ADMIN");
        m.Setup(u => u.IsAdmin).Returns(true);
        return m;
    }

    private static Mock<ICurrentUserService> MemberUser(string memberId = "AMB-001")
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.MemberId).Returns(memberId);
        m.Setup(u => u.IsAdmin).Returns(false);
        return m;
    }

    private static Ticket BuildTicket(
        string id,
        string memberId = "AMB-001",
        TicketStatus status = TicketStatus.Open) => new()
    {
        Id = id,
        TicketNumber = $"HD-20260401-{id}",
        MemberId = memberId,
        Subject = $"Ticket {id}",
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
    public async Task GetTickets_WhenNoTickets_ReturnsEmptyPagedResult()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new GetTicketsHandler(db, AdminUser().Object);

        var result = await handler.Handle(
            new GetTicketsQuery(Page(), null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetTickets_WhenAdmin_ReturnsAllTickets()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddRangeAsync(
            BuildTicket("TKT-001", "AMB-001"),
            BuildTicket("TKT-002", "AMB-002"),
            BuildTicket("TKT-003", "AMB-003")
        );
        await db.SaveChangesAsync();

        var handler = new GetTicketsHandler(db, AdminUser().Object);

        var result = await handler.Handle(
            new GetTicketsQuery(Page(), null), CancellationToken.None);

        result.Value!.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetTickets_WhenMember_ReturnsOnlyOwnTickets()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddRangeAsync(
            BuildTicket("TKT-001", "AMB-001"),
            BuildTicket("TKT-002", "AMB-001"),
            BuildTicket("TKT-003", "AMB-002")
        );
        await db.SaveChangesAsync();

        var handler = new GetTicketsHandler(db, MemberUser("AMB-001").Object);

        var result = await handler.Handle(
            new GetTicketsQuery(Page(), null), CancellationToken.None);

        result.Value!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetTickets_WhenStatusFilter_ReturnsOnlyMatchingStatus()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddRangeAsync(
            BuildTicket("TKT-001", status: TicketStatus.Open),
            BuildTicket("TKT-002", status: TicketStatus.Resolved),
            BuildTicket("TKT-003", status: TicketStatus.Open)
        );
        await db.SaveChangesAsync();

        var handler = new GetTicketsHandler(db, AdminUser().Object);

        var result = await handler.Handle(
            new GetTicketsQuery(Page(), "Open"), CancellationToken.None);

        result.Value!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetTickets_ReturnsPaginatedMetadata()
    {
        await using var db = InMemoryDbHelper.Create();
        for (int i = 0; i < 7; i++)
            await db.Tickets.AddAsync(BuildTicket($"TKT-{i:D3}"));
        await db.SaveChangesAsync();

        var handler = new GetTicketsHandler(db, AdminUser().Object);

        var result = await handler.Handle(
            new GetTicketsQuery(Page(page: 2, size: 3), null), CancellationToken.None);

        result.Value!.TotalCount.Should().Be(7);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(3);
    }
}
