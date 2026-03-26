using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.Signups.Features.Placement.Commands.PlaceMember;
using MLMConquerorGlobalEdition.Signups.Tests.Helpers;

namespace MLMConquerorGlobalEdition.Signups.Tests.Features.Placement;

public class PlaceMemberHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> DateTimeAt(DateTime now)
    {
        var mock = new Mock<IDateTimeProvider>();
        mock.Setup(d => d.Now).Returns(now);
        return mock;
    }

    private static MemberProfile BuildMember(string memberId, DateTime enrollDate) => new()
    {
        MemberId = memberId,
        FirstName = "Test",
        LastName = "Member",
        MemberType = MemberType.Ambassador,
        EnrollDate = enrollDate,
        Country = "US",
        CreatedBy = "seed",
        LastUpdateDate = FixedNow
    };

    [Fact]
    public async Task Handle_WhenMemberNotFound_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object);

        var result = await handler.Handle(
            new PlaceMemberCommand("NON-EXISTENT", "AMB-000001", "Left"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenPlacementWindowExpired_ThrowsPlacementWindowExpiredException()
    {
        await using var db = InMemoryDbHelper.Create();
        var enrollDate = FixedNow.AddDays(-31); // 31 days ago — window expired
        await db.MemberProfiles.AddAsync(BuildMember("AMB-000002", enrollDate));
        await db.SaveChangesAsync();

        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object);

        Func<Task> act = () => handler.Handle(
            new PlaceMemberCommand("AMB-000002", "AMB-000001", "Left"), CancellationToken.None);

        await act.Should().ThrowAsync<PlacementWindowExpiredException>();
    }

    [Fact]
    public async Task Handle_WhenParentMemberNotFound_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var enrollDate = FixedNow.AddDays(-5); // within window
        await db.MemberProfiles.AddAsync(BuildMember("AMB-000002", enrollDate));
        await db.SaveChangesAsync();

        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object);

        var result = await handler.Handle(
            new PlaceMemberCommand("AMB-000002", "NON-EXISTENT-PARENT", "Left"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PARENT_MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenPositionAlreadyOccupied_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var enrollDate = FixedNow.AddDays(-5);

        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-000002", enrollDate),
            BuildMember("AMB-000001", FixedNow.AddDays(-60))
        );
        // Left position under AMB-000001 already occupied by AMB-000003
        await db.DualTeamTree.AddAsync(new DualTeamEntity
        {
            MemberId = "AMB-000003",
            ParentMemberId = "AMB-000001",
            Side = TreeSide.Left,
            HierarchyPath = "/AMB-000001/AMB-000003",
            CreatedBy = "seed",
            LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object);

        var result = await handler.Handle(
            new PlaceMemberCommand("AMB-000002", "AMB-000001", "Left"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("POSITION_OCCUPIED");
    }

    [Fact]
    public async Task Handle_WhenValidPlacement_CreatesDualTeamNodeAndPlacementLog()
    {
        await using var db = InMemoryDbHelper.Create();
        var enrollDate = FixedNow.AddDays(-5);

        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-000002", enrollDate),
            BuildMember("AMB-000001", FixedNow.AddDays(-60))
        );
        await db.DualTeamTree.AddAsync(new DualTeamEntity
        {
            MemberId = "AMB-000001",
            ParentMemberId = null,
            Side = TreeSide.Left,
            HierarchyPath = "/AMB-000001",
            CreatedBy = "seed",
            LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object);

        var result = await handler.Handle(
            new PlaceMemberCommand("AMB-000002", "AMB-000001", "Right"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        var node = db.DualTeamTree.FirstOrDefault(n => n.MemberId == "AMB-000002");
        node.Should().NotBeNull();
        node!.ParentMemberId.Should().Be("AMB-000001");
        node.Side.Should().Be(TreeSide.Right);
        node.HierarchyPath.Should().Be("/AMB-000001/AMB-000002");

        var log = db.PlacementLogs.FirstOrDefault(l => l.MemberId == "AMB-000002");
        log.Should().NotBeNull();
        log!.Action.Should().Be("Placed");
        log.Side.Should().Be(TreeSide.Right);
    }

    [Fact]
    public async Task Handle_WhenMemberEnrolledExactly30DaysAgo_Succeeds()
    {
        // Boundary: exactly 30 days — TotalDays == 30 — NOT > 30, so allowed
        await using var db = InMemoryDbHelper.Create();
        var enrollDate = FixedNow.AddDays(-30);

        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-000002", enrollDate),
            BuildMember("AMB-000001", FixedNow.AddDays(-60))
        );
        await db.SaveChangesAsync();

        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object);

        var result = await handler.Handle(
            new PlaceMemberCommand("AMB-000002", "AMB-000001", "Left"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
