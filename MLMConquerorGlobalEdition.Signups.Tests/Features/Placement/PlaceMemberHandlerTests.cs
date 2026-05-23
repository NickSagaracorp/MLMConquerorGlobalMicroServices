using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Repository.Services.Trees;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Features.Placement.Commands.PlaceMember;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Placement;

public class PlaceMemberHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> DateTimeAt(DateTime now)
    {
        var mock = new Mock<IDateTimeProvider>();
        mock.Setup(d => d.Now).Returns(now);
        return mock;
    }

    private static Mock<IPushNotificationService> NullPush()
    {
        var m = new Mock<IPushNotificationService>();
        m.Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                                 It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .Returns(Task.CompletedTask);
        return m;
    }

    private static Mock<IDualTeamPointsRecalculator> NullLegPoints()
    {
        var m = new Mock<IDualTeamPointsRecalculator>();
        m.Setup(r => r.RecalculateForUplinesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .Returns(Task.CompletedTask);
        return m;
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
        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object, NullPush().Object, NullLegPoints().Object);

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

        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object, NullPush().Object, NullLegPoints().Object);

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

        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object, NullPush().Object, NullLegPoints().Object);

        var result = await handler.Handle(
            new PlaceMemberCommand("AMB-000002", "NON-EXISTENT-PARENT", "Left"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PARENT_MEMBER_NOT_FOUND");
    }

    /// <summary>
    /// Sprint-15 Bug B — depth-guard test. If every candidate node in the BFS
    /// has a HierarchyPath already past the 1500-byte safety cap, the handler
    /// must refuse the placement with NO_AVAILABLE_SLOT rather than push the
    /// SQL nonclustered index over its 1700-byte limit (the exact failure mode
    /// that surfaced in the 88-signup load test). We synthesize a path long
    /// enough to trigger the guard by padding the member ID with filler.
    /// </summary>
    [Fact]
    public async Task Handle_WhenAllCandidatesExceedHierarchyDepthCap_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var enrollDate = FixedNow.AddDays(-5);

        // Synthesize a parent with a HierarchyPath already over the 1500-byte cap
        // by giving it a fake long ID — proves the guard would prevent even a
        // direct placement under it.
        var longId = "AMB-DEEP-" + new string('X', 1500);
        var longParentPath = $"/{longId}/";

        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-000002", enrollDate),
            BuildMember(longId, FixedNow.AddDays(-60))
        );
        await db.DualTeamTree.AddAsync(new DualTeamEntity {
            MemberId       = longId,
            ParentMemberId = null,
            Side           = TreeSide.Left,
            HierarchyPath  = longParentPath,
            CreatedBy      = "seed",
            LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object, NullPush().Object, NullLegPoints().Object);

        var result = await handler.Handle(
            new PlaceMemberCommand("AMB-000002", longId, "Left"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_AVAILABLE_SLOT");
    }

    /// <summary>
    /// Sprint-15 Bug B — when the requested slot is occupied, BFS descends into
    /// the subtree and places the member at the first available matching-side slot.
    /// </summary>
    [Fact]
    public async Task Handle_WhenRequestedLeftIsOccupied_BfsFindsDeeperLeftSlot()
    {
        await using var db = InMemoryDbHelper.Create();
        var enrollDate = FixedNow.AddDays(-5);

        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-000002", enrollDate),
            BuildMember("AMB-000001", FixedNow.AddDays(-60))
        );
        // Root's Left slot is occupied. AMB-000003 (the existing left child) has its own Left slot empty.
        // BFS should find that empty slot and place AMB-000002 there.
        await db.DualTeamTree.AddRangeAsync(
            new DualTeamEntity { MemberId = "AMB-000001", ParentMemberId = null,
                                  Side = TreeSide.Left,  HierarchyPath = "/AMB-000001/",
                                  CreatedBy = "seed", LastUpdateDate = FixedNow },
            new DualTeamEntity { MemberId = "AMB-000003", ParentMemberId = "AMB-000001",
                                  Side = TreeSide.Left,  HierarchyPath = "/AMB-000001/AMB-000003/",
                                  CreatedBy = "seed", LastUpdateDate = FixedNow }
        );
        await db.SaveChangesAsync();

        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object, NullPush().Object, NullLegPoints().Object);

        var result = await handler.Handle(
            new PlaceMemberCommand("AMB-000002", "AMB-000001", "Left"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var node = db.DualTeamTree.FirstOrDefault(n => n.MemberId == "AMB-000002");
        node.Should().NotBeNull();
        node!.ParentMemberId.Should().Be("AMB-000003"); // BFS placed under existing left child
        node.Side.Should().Be(TreeSide.Left);
        node.HierarchyPath.Should().Be("/AMB-000001/AMB-000003/AMB-000002/");
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
            HierarchyPath = "/AMB-000001/",
            CreatedBy = "seed",
            LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object, NullPush().Object, NullLegPoints().Object);

        var result = await handler.Handle(
            new PlaceMemberCommand("AMB-000002", "AMB-000001", "Right"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        var node = db.DualTeamTree.FirstOrDefault(n => n.MemberId == "AMB-000002");
        node.Should().NotBeNull();
        node!.ParentMemberId.Should().Be("AMB-000001");
        node.Side.Should().Be(TreeSide.Right);
        node.HierarchyPath.Should().Be("/AMB-000001/AMB-000002/");

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

        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object, NullPush().Object, NullLegPoints().Object);

        var result = await handler.Handle(
            new PlaceMemberCommand("AMB-000002", "AMB-000001", "Left"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Sprint-15 Bug C verification — every successful placement must invoke
    /// the shared <see cref="IDualTeamPointsRecalculator"/> with the actual
    /// PARENT id we placed under (BFS may have chosen a deeper parent than
    /// the originally requested PlaceUnderMemberId).
    /// </summary>
    [Fact]
    public async Task Handle_WhenSuccessful_InvokesLegPointsRecalculator()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-000002", FixedNow.AddDays(-5)),
            BuildMember("AMB-000001", FixedNow.AddDays(-60))
        );
        await db.DualTeamTree.AddAsync(new DualTeamEntity {
            MemberId = "AMB-000001", ParentMemberId = null, Side = TreeSide.Left,
            HierarchyPath = "/AMB-000001/", CreatedBy = "seed", LastUpdateDate = FixedNow });
        await db.SaveChangesAsync();

        var legPoints = NullLegPoints();
        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object, NullPush().Object, legPoints.Object);

        var result = await handler.Handle(
            new PlaceMemberCommand("AMB-000002", "AMB-000001", "Left"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        legPoints.Verify(r => r.RecalculateForUplinesAsync(
            "AMB-000001", It.IsAny<CancellationToken>()), Times.Once);
    }
}
