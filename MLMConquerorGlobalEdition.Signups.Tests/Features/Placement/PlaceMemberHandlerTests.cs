using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
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

    private static MemberProfile BuildMember(string memberId, DateTime enrollDate) => new()
    {
        MemberId = memberId,
        // Revision I8: el destino tiene que ser el patrocinador o su red; en estas pruebas, AMB-000001.
        SponsorMemberId = memberId == "AMB-000001" ? null : "AMB-000001",
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
        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object, NullPush().Object);

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

        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object, NullPush().Object);

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

        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object, NullPush().Object);

        var result = await handler.Handle(
            new PlaceMemberCommand("AMB-000002", "NON-EXISTENT-PARENT", "Left"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PARENT_MEMBER_NOT_FOUND");
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

        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object, NullPush().Object);

        var result = await handler.Handle(
            new PlaceMemberCommand("AMB-000002", "AMB-000001", "Left"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var node = db.DualTeamTree.FirstOrDefault(n => n.MemberId == "AMB-000002");
        node.Should().NotBeNull();
        node!.ParentMemberId.Should().Be("AMB-000003"); // BFS placed under existing left child
        node.Side.Should().Be(TreeSide.Left);
        node.Depth.Should().Be(2);
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

        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object, NullPush().Object);

        var result = await handler.Handle(
            new PlaceMemberCommand("AMB-000002", "AMB-000001", "Right"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        var node = db.DualTeamTree.FirstOrDefault(n => n.MemberId == "AMB-000002");
        node.Should().NotBeNull();
        node!.ParentMemberId.Should().Be("AMB-000001");
        node.Side.Should().Be(TreeSide.Right);
        node.Depth.Should().Be(1);

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

        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object, NullPush().Object);

        var result = await handler.Handle(
            new PlaceMemberCommand("AMB-000002", "AMB-000001", "Left"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Sprint-15 Bug 10 — defensive HierarchyPath normalize. Legacy parent
    /// rows historically existed where HierarchyPath was missing its trailing
    /// slash (e.g. "/AMB-000001"). Concatenating "{path}{newId}/" against such
    /// a parent glued IDs together: "/AMB-000001AMB-000002/". The handler now
    /// uses TrimEnd('/') + '/' to guarantee exactly one separator regardless.
    /// We simulate the malformed state by inserting a parent row directly into
    /// the in-memory DB with no trailing slash, then placing under it.
    /// </summary>
    [Fact]
    public async Task Handle_WhenSponsorPathMissingTrailingSlash_NewPathHasSlashBetweenIds()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-000002", FixedNow.AddDays(-5)),
            BuildMember("AMB-000001", FixedNow.AddDays(-60))
        );
        // MALFORMED parent — no trailing slash on HierarchyPath.
        await db.DualTeamTree.AddAsync(new DualTeamEntity {
            MemberId       = "AMB-000001",
            ParentMemberId = null,
            Side           = TreeSide.Left,
            HierarchyPath  = "/AMB-000001",
            CreatedBy      = "seed",
            LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var handler = new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object, NullPush().Object);

        var result = await handler.Handle(
            new PlaceMemberCommand("AMB-000002", "AMB-000001", "Left"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var node = db.DualTeamTree.First(n => n.MemberId == "AMB-000002");
        // Plan Q: el binario ya no tiene ruta de texto; la posicion es el padre, el lado y la profundidad.
        node.ParentMemberId.Should().Be("AMB-000001");
        node.Side.Should().Be(TreeSide.Left);
        node.Depth.Should().Be(1);
    }

    /// <summary>
    /// Plan P (revision I3): ya no hay tope de 1.500 caracteres de ruta (comparaba caracteres, no bytes). El limite
    /// real es la clave compacta: un padre con una ruta muy larga se puede usar.
    /// </summary>
    [Fact]
    public async Task Handle_WhenParentPathIsVeryLong_StillPlaces()
    {
        await using var db = InMemoryDbHelper.Create();
        var longId = "AMB-DEEP-" + new string('X', 1500);
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-000002", FixedNow.AddDays(-5)),
            BuildMember(longId, FixedNow.AddDays(-60)));
        await db.DualTeamTree.AddAsync(new DualTeamEntity {
            MemberId = longId, ParentMemberId = null, Side = TreeSide.Left,
            HierarchyPath = $"/{longId}/", CreatedBy = "seed", LastUpdateDate = FixedNow });
        await db.SaveChangesAsync();

        var result = await new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object, NullPush().Object)
            .Handle(new PlaceMemberCommand("AMB-000002", longId, "Left", ByStaff: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        db.DualTeamTree.First(n => n.MemberId == "AMB-000002").ParentMemberId.Should().Be(longId);
    }

    /// <summary>Plan P (C2): las piernas suben de forma incremental con los puntos del colocado.</summary>
    [Fact]
    public async Task Handle_WhenSuccessful_AddsThePointsToTheParentLeg()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-000002", FixedNow.AddDays(-5)),
            BuildMember("AMB-000001", FixedNow.AddDays(-60)));
        await db.MemberStatistics.AddAsync(new MemberStatisticEntity { MemberId = "AMB-000002", PersonalPoints = 40, CreatedBy = "seed" });
        await db.DualTeamTree.AddAsync(new DualTeamEntity {
            MemberId = "AMB-000001", ParentMemberId = null, Side = TreeSide.Left,
            HierarchyPath = "/AMB-000001/", CreatedBy = "seed", LastUpdateDate = FixedNow });
        await db.SaveChangesAsync();

        var result = await new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object, NullPush().Object)
            .Handle(new PlaceMemberCommand("AMB-000002", "AMB-000001", "Left"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var padre = db.DualTeamTree.First(n => n.MemberId == "AMB-000001");
        padre.LeftLegPoints.Should().Be(40);
        padre.LeftLegCount.Should().Be(1);
    }

    /// <summary>Revision I8: no se puede colgar bajo un nodo que no es del patrocinador ni de su red.</summary>
    [Fact]
    public async Task Handle_WhenTargetIsOutsideTheSponsorNetwork_IsRejected()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-000002", FixedNow.AddDays(-5)),
            BuildMember("AMB-000001", FixedNow.AddDays(-60)),
            BuildMember("AMB-AJENO", FixedNow.AddDays(-60)));
        await db.SaveChangesAsync();

        var result = await new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object, NullPush().Object)
            .Handle(new PlaceMemberCommand("AMB-000002", "AMB-AJENO", "Left"), CancellationToken.None);

        result.ErrorCode.Should().Be("PLACEMENT_TARGET_NOT_ALLOWED");
    }

    /// <summary>Un nodo que ya tiene padre sigue siendo ALREADY_PLACED.</summary>
    [Fact]
    public async Task Handle_WhenNodeHasAParent_IsAlreadyPlaced()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-000002", FixedNow.AddDays(-5)),
            BuildMember("AMB-000001", FixedNow.AddDays(-60)));
        await db.DualTeamTree.AddRangeAsync(
            new DualTeamEntity { MemberId = "AMB-000001", Side = TreeSide.Left, HierarchyPath = "/AMB-000001/", CreatedBy = "seed", LastUpdateDate = FixedNow },
            new DualTeamEntity { MemberId = "AMB-000002", ParentMemberId = "AMB-000001", Side = TreeSide.Left, HierarchyPath = "/AMB-000001/AMB-000002/", CreatedBy = "seed", LastUpdateDate = FixedNow });
        await db.SaveChangesAsync();

        var result = await new PlaceMemberHandler(db, DateTimeAt(FixedNow).Object, NullPush().Object)
            .Handle(new PlaceMemberCommand("AMB-000002", "AMB-000001", "Right"), CancellationToken.None);

        result.ErrorCode.Should().Be("ALREADY_PLACED");
    }
}
