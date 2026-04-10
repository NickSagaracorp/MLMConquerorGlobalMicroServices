using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.GetTicket;
using MLMConquerorGlobalEdition.TicketManagementSystem.Services;
using MLMConquerorGlobalEdition.TicketManagementSystem.Tests.Helpers;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Tests.Features.GetTicket;

public class GetTicketHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

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

    private static Ticket BuildTicket(string id = "TKT-001", string memberId = "AMB-001") => new()
    {
        Id = id,
        TicketNumber = "HD-20260401-0001",
        MemberId = memberId,
        Subject = "Test Issue",
        Body = "Something broke",
        Status = TicketStatus.Open,
        CategoryId = 1,
        Priority = TicketPriority.Normal,
        Channel = TicketChannel.Portal,
        CreationDate = FixedNow,
        CreatedBy = "seed",
        LastUpdateDate = FixedNow
    };

    [Fact]
    public async Task GetTicket_WhenNotFound_ReturnsTicketNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new GetTicketHandler(db, AdminUser().Object);

        var result = await handler.Handle(
            new GetTicketQuery("TKT-GHOST"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TICKET_NOT_FOUND");
    }

    [Fact]
    public async Task GetTicket_WhenNonAdminAccessesOtherMemberTicket_ReturnsForbiddenFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.TicketCategories.AddAsync(new TicketCategory
        {
            Id = 1, Name = "General", CreationDate = FixedNow, CreatedBy = "seed"
        });
        await db.Tickets.AddAsync(BuildTicket("TKT-001", "AMB-OTHER"));
        await db.SaveChangesAsync();

        var handler = new GetTicketHandler(db, MemberUser("AMB-001").Object);

        var result = await handler.Handle(
            new GetTicketQuery("TKT-001"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task GetTicket_WhenAdmin_ReturnsFullDto()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.TicketCategories.AddAsync(new TicketCategory
        {
            Id = 1, Name = "General", CreationDate = FixedNow, CreatedBy = "seed"
        });
        await db.Tickets.AddAsync(BuildTicket("TKT-001", "AMB-001"));
        await db.SaveChangesAsync();

        var handler = new GetTicketHandler(db, AdminUser().Object);

        var result = await handler.Handle(
            new GetTicketQuery("TKT-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Subject.Should().Be("Test Issue");
        result.Value.MemberId.Should().Be("AMB-001");
        result.Value.CategoryName.Should().Be("General");
        result.Value.Comments.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTicket_WhenMemberViewsOwnTicket_HidesInternalComments()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.TicketCategories.AddAsync(new TicketCategory
        {
            Id = 1, Name = "General", CreationDate = FixedNow, CreatedBy = "seed"
        });
        await db.Tickets.AddAsync(BuildTicket("TKT-001", "AMB-001"));
        await db.SaveChangesAsync();

        await db.TicketComments.AddRangeAsync(
            new TicketComment
            {
                TicketId = "TKT-001", AuthorId = "agent-001", AuthorType = "agent",
                Body = "Internal note", IsInternal = true,
                CreationDate = FixedNow, CreatedBy = "agent-001"
            },
            new TicketComment
            {
                TicketId = "TKT-001", AuthorId = "agent-001", AuthorType = "agent",
                Body = "Public reply", IsInternal = false,
                CreationDate = FixedNow, CreatedBy = "agent-001"
            }
        );
        await db.SaveChangesAsync();

        var handler = new GetTicketHandler(db, MemberUser("AMB-001").Object);

        var result = await handler.Handle(
            new GetTicketQuery("TKT-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Comments.Should().HaveCount(1);
        result.Value.Comments.Single().Content.Should().Be("Public reply");
    }

    [Fact]
    public async Task GetTicket_WhenAdminViews_ShowsInternalComments()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.TicketCategories.AddAsync(new TicketCategory
        {
            Id = 1, Name = "General", CreationDate = FixedNow, CreatedBy = "seed"
        });
        await db.Tickets.AddAsync(BuildTicket("TKT-001", "AMB-001"));
        await db.SaveChangesAsync();

        await db.TicketComments.AddRangeAsync(
            new TicketComment
            {
                TicketId = "TKT-001", AuthorId = "agent-001", AuthorType = "agent",
                Body = "Internal note", IsInternal = true,
                CreationDate = FixedNow, CreatedBy = "agent-001"
            },
            new TicketComment
            {
                TicketId = "TKT-001", AuthorId = "agent-001", AuthorType = "agent",
                Body = "Public reply", IsInternal = false,
                CreationDate = FixedNow, CreatedBy = "agent-001"
            }
        );
        await db.SaveChangesAsync();

        var handler = new GetTicketHandler(db, AdminUser().Object);

        var result = await handler.Handle(
            new GetTicketQuery("TKT-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Comments.Should().HaveCount(2);
    }
}
