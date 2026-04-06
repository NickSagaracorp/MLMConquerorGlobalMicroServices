using MLMConquerorGlobalEdition.AdminAPI.Features.Placement.RunAutoPlacement;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Placement;

public class RunAutoPlacementHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 6, 10, 0, 0, DateTimeKind.Utc);

    private static Mock<ICurrentUserService> CurrentUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(x => x.UserId).Returns("admin-001");
        return m;
    }

    private static Mock<IDateTimeProvider> Clock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(x => x.Now).Returns(FixedNow);
        return m;
    }

    private static MemberProfile BuildProfile(string memberId, string? sponsorId = null,
        int enrolledDaysAgo = 35) => new()
    {
        MemberId        = memberId,
        FirstName       = "Test",
        LastName        = memberId,
        MemberType      = MemberType.Ambassador,
        Status          = MemberAccountStatus.Active,
        EnrollDate      = FixedNow.AddDays(-enrolledDaysAgo), // expired window by default
        SponsorMemberId = sponsorId,
        CreationDate    = FixedNow,
        LastUpdateDate  = FixedNow,
        CreatedBy       = "seed"
    };

    private static DualTeamEntity BuildNode(string memberId, string? parentId = null,
        TreeSide side = TreeSide.Left, string? path = null) => new()
    {
        MemberId       = memberId,
        ParentMemberId = parentId,
        Side           = side,
        HierarchyPath  = path ?? $"/{memberId}/",
        CreationDate   = FixedNow,
        LastUpdateDate = FixedNow,
        CreatedBy      = "seed",
        LastUpdateBy   = "seed"
    };

    private static RunAutoPlacementHandler CreateHandler(
        Repository.Context.AppDbContext db) =>
        new(db, CurrentUser().Object, Clock().Object);

    [Fact]
    public async Task Handle_WhenNoExpiredUnplacedMembers_ReturnsZeroPlaced()
    {
        await using var db = InMemoryDbHelper.Create();

        var result = await CreateHandler(db).Handle(
            new RunAutoPlacementCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PlacedCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenAllExpiredMembersAlreadyInTree_ReturnsZeroPlaced()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(BuildProfile("amb-001", "sponsor-001", enrolledDaysAgo: 35));
        db.DualTeamTree.Add(BuildNode("amb-001")); // already placed
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new RunAutoPlacementCommand(), CancellationToken.None);

        result.Value!.PlacedCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenSponsorHasNoLeftChild_PlacesMemberOnLeft()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(BuildProfile("amb-001", "sponsor-001", enrolledDaysAgo: 35));
        db.DualTeamTree.Add(BuildNode("sponsor-001")); // sponsor in tree, no children
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new RunAutoPlacementCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PlacedCount.Should().Be(1);

        var node = db.DualTeamTree.First(d => d.MemberId == "amb-001");
        node.Side.Should().Be(TreeSide.Left);
        node.ParentMemberId.Should().Be("sponsor-001");
    }

    [Fact]
    public async Task Handle_WhenSponsorHasLeftChildButNoRight_PlacesMemberOnRight()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(BuildProfile("amb-001", "sponsor-001", enrolledDaysAgo: 35));
        db.DualTeamTree.AddRange(
            BuildNode("sponsor-001"),
            BuildNode("existing-left", "sponsor-001", TreeSide.Left, "/sponsor-001/existing-left/")
        );
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new RunAutoPlacementCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PlacedCount.Should().Be(1);

        var node = db.DualTeamTree.First(d => d.MemberId == "amb-001");
        node.Side.Should().Be(TreeSide.Right);
    }

    [Fact]
    public async Task Handle_WhenSponsorNotInTree_SkipsMember()
    {
        await using var db = InMemoryDbHelper.Create();
        // Sponsor exists in profiles but NOT in dual tree
        db.MemberProfiles.AddRange(
            BuildProfile("amb-001", "sponsor-001", enrolledDaysAgo: 35),
            BuildProfile("sponsor-001", enrolledDaysAgo: 100)
        );
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new RunAutoPlacementCommand(), CancellationToken.None);

        result.Value!.PlacedCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenMembersWindowHasNotExpired_DoesNotAutoPlace()
    {
        await using var db = InMemoryDbHelper.Create();
        // enrolled only 5 days ago — window is 30 days, so NOT expired
        db.MemberProfiles.Add(BuildProfile("amb-001", "sponsor-001", enrolledDaysAgo: 5));
        db.DualTeamTree.Add(BuildNode("sponsor-001"));
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new RunAutoPlacementCommand(), CancellationToken.None);

        result.Value!.PlacedCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenMemberPlaced_LogsAutoPlacedAction()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(BuildProfile("amb-001", "sponsor-001", enrolledDaysAgo: 35));
        db.DualTeamTree.Add(BuildNode("sponsor-001"));
        await db.SaveChangesAsync();

        await CreateHandler(db).Handle(new RunAutoPlacementCommand(), CancellationToken.None);

        db.PlacementLogs.Any(p => p.MemberId == "amb-001" && p.Action == "AutoPlaced")
          .Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenMultipleUnplacedMembers_PlacesAll()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.AddRange(
            BuildProfile("amb-001", "sponsor-001", enrolledDaysAgo: 35),
            BuildProfile("amb-002", "sponsor-001", enrolledDaysAgo: 40)
        );
        db.DualTeamTree.Add(BuildNode("sponsor-001"));
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new RunAutoPlacementCommand(), CancellationToken.None);

        result.Value!.PlacedCount.Should().Be(2);
        result.Value.PlacedMemberIds.Should().Contain("amb-001").And.Contain("amb-002");
    }
}
