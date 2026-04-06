using MLMConquerorGlobalEdition.AdminAPI.Features.Placement.GetAdminPendingPlacements;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Placement;

public class GetAdminPendingPlacementsHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 6, 10, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> Clock() =>
        new Mock<IDateTimeProvider>().Also(m => m.Setup(x => x.Now).Returns(FixedNow));

    private static MemberProfile BuildProfile(string memberId, string? sponsorId = null,
        int enrolledDaysAgo = 5) => new()
    {
        MemberId        = memberId,
        FirstName       = "Test",
        LastName        = memberId,
        MemberType      = MemberType.Ambassador,
        Status          = MemberAccountStatus.Active,
        EnrollDate      = FixedNow.AddDays(-enrolledDaysAgo),
        SponsorMemberId = sponsorId,
        CreationDate    = FixedNow,
        LastUpdateDate  = FixedNow,
        CreatedBy       = "seed"
    };

    private static GetAdminPendingPlacementsHandler CreateHandler(
        Repository.Context.AppDbContext db) =>
        new(db, Clock().Object);

    [Fact]
    public async Task Handle_WhenNoMembersInWindow_ReturnsEmpty()
    {
        await using var db = InMemoryDbHelper.Create();

        var result = await CreateHandler(db).Handle(
            new GetAdminPendingPlacementsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenMembersEnrolledWithin30Days_ReturnsThemAsPending()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.AddRange(
            BuildProfile("amb-001", enrolledDaysAgo: 5),
            BuildProfile("amb-002", enrolledDaysAgo: 15)
        );
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new GetAdminPendingPlacementsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WhenMemberEnrolledMoreThan30DaysAgo_IsNotIncluded()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(BuildProfile("old-member", enrolledDaysAgo: 35));
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new GetAdminPendingPlacementsQuery(), CancellationToken.None);

        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenMemberIsInDualTree_IsMarkedAsPlaced()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(BuildProfile("amb-001", enrolledDaysAgo: 5));
        db.DualTeamTree.Add(new DualTeamEntity
        {
            MemberId       = "amb-001",
            ParentMemberId = "sponsor-001",
            Side           = TreeSide.Left,
            HierarchyPath  = "/sponsor-001/amb-001/",
            CreationDate   = FixedNow,
            LastUpdateDate = FixedNow,
            CreatedBy      = "seed",
            LastUpdateBy   = "seed"
        });
        db.PlacementLogs.Add(new PlacementLog
        {
            MemberId            = "amb-001",
            PlacedUnderMemberId = "sponsor-001",
            Side                = TreeSide.Left,
            Action              = "Placed",
            UnplacementCount    = 0,
            FirstPlacementDate  = FixedNow.AddDays(-2),
            CreationDate        = FixedNow,
            CreatedBy           = "seed"
        });
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new GetAdminPendingPlacementsQuery(), CancellationToken.None);

        var dto = result.Value!.First();
        dto.IsAlreadyPlaced.Should().BeTrue();
        dto.PlacementStatus.Should().Be("Placed");
    }

    [Fact]
    public async Task Handle_WhenFilteredBySponsorId_ReturnsOnlyThatSponsorMembers()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.AddRange(
            BuildProfile("amb-001", sponsorId: "sponsor-A", enrolledDaysAgo: 5),
            BuildProfile("amb-002", sponsorId: "sponsor-B", enrolledDaysAgo: 5)
        );
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new GetAdminPendingPlacementsQuery(SponsorId: "sponsor-A"), CancellationToken.None);

        result.Value!.Should().HaveCount(1);
        result.Value.First().MemberId.Should().Be("amb-001");
    }

    [Fact]
    public async Task Handle_WhenMemberHasUsedMaxOpportunities_IsMarkedBlocked()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(BuildProfile("amb-001", enrolledDaysAgo: 5));
        db.PlacementLogs.Add(new PlacementLog
        {
            MemberId            = "amb-001",
            PlacedUnderMemberId = "parent",
            Side                = TreeSide.Left,
            Action              = "Placed",
            UnplacementCount    = 2,  // max = 2 → blocked
            FirstPlacementDate  = FixedNow.AddDays(-4),
            CreationDate        = FixedNow,
            CreatedBy           = "seed"
        });
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new GetAdminPendingPlacementsQuery(), CancellationToken.None);

        var dto = result.Value!.First();
        dto.IsBlocked.Should().BeTrue();
        dto.PlacementStatus.Should().Be("Blocked");
    }
}

file static class MockExtensions
{
    public static Mock<T> Also<T>(this Mock<T> mock, Action<Mock<T>> configure) where T : class
    {
        configure(mock);
        return mock;
    }
}
