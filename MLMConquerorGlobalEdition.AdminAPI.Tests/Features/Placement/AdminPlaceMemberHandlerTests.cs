using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Features.Placement.AdminPlaceMember;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Placement;

public class AdminPlaceMemberHandlerTests
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

    private static MemberProfile BuildProfile(string memberId) => new()
    {
        MemberId       = memberId,
        FirstName      = "Test",
        LastName       = memberId,
        MemberType     = MemberType.Ambassador,
        Status         = MemberAccountStatus.Active,
        EnrollDate     = FixedNow.AddDays(-5),
        CreationDate   = FixedNow,
        LastUpdateDate = FixedNow,
        CreatedBy      = "seed"
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

    private static AdminPlaceMemberHandler CreateHandler(
        Repository.Context.AppDbContext db) =>
        new(db, CurrentUser().Object, Clock().Object);

    [Fact]
    public async Task Handle_WhenMemberToPlaceDoesNotExist_ReturnsMemberNotFound()
    {
        await using var db = InMemoryDbHelper.Create();

        var result = await CreateHandler(db).Handle(
            new AdminPlaceMemberCommand("missing-member", "parent-001", "Left"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenTargetParentNotInDualTree_AutoCreatesParentAsRoot()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(BuildProfile("amb-001"));
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new AdminPlaceMemberCommand("amb-001", "nonexistent-parent", "Left"),
            CancellationToken.None);

        // Admin placement auto-creates the target parent as a root node when it
        // doesn't yet exist in the dual tree (first placement under an upline
        // ambassador who was never placed themselves).
        result.IsSuccess.Should().BeTrue();
        var parent = db.DualTeamTree.FirstOrDefault(d => d.MemberId == "nonexistent-parent");
        parent.Should().NotBeNull();
        parent!.ParentMemberId.Should().BeNull();
        parent.HierarchyPath.Should().Be("/nonexistent-parent/");

        var placed = db.DualTeamTree.FirstOrDefault(d => d.MemberId == "amb-001");
        placed.Should().NotBeNull();
        placed!.ParentMemberId.Should().Be("nonexistent-parent");
        placed.HierarchyPath.Should().Be("/nonexistent-parent/amb-001/");
    }

    [Fact]
    public async Task Handle_WhenSlotIsOccupied_ReturnsSlotOccupied()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.AddRange(BuildProfile("amb-001"), BuildProfile("existing-child"));
        db.DualTeamTree.AddRange(
            BuildNode("parent-001", null, TreeSide.Left, "/parent-001/"),
            BuildNode("existing-child", "parent-001", TreeSide.Left, "/parent-001/existing-child/")
        );
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new AdminPlaceMemberCommand("amb-001", "parent-001", "Left"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SLOT_OCCUPIED");
    }

    [Fact]
    public async Task Handle_WhenTargetParentIsSameMember_ReturnsAutoSuperiority()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(BuildProfile("amb-001"));
        db.DualTeamTree.Add(BuildNode("amb-001", null, TreeSide.Left, "/amb-001/"));
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new AdminPlaceMemberCommand("amb-001", "amb-001", "Left"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("AUTO_SUPERIORITY");
    }

    [Fact]
    public async Task Handle_WhenValidPlacement_CreatesNodeAndPlacementLog()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(BuildProfile("amb-001"));
        db.DualTeamTree.Add(BuildNode("parent-001", null, TreeSide.Left, "/parent-001/"));
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new AdminPlaceMemberCommand("amb-001", "parent-001", "Right"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        db.DualTeamTree.Any(d => d.MemberId == "amb-001" && d.ParentMemberId == "parent-001")
          .Should().BeTrue();
        db.PlacementLogs.Any(p => p.MemberId == "amb-001" && p.Action == "Placed")
          .Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenValidPlacement_SetsCorrectHierarchyPath()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(BuildProfile("amb-001"));
        db.DualTeamTree.Add(BuildNode("parent-001", null, TreeSide.Left, "/parent-001/"));
        await db.SaveChangesAsync();

        await CreateHandler(db).Handle(
            new AdminPlaceMemberCommand("amb-001", "parent-001", "Left"),
            CancellationToken.None);

        var node = db.DualTeamTree.First(d => d.MemberId == "amb-001");
        node.HierarchyPath.Should().Be("/parent-001/amb-001/");
    }

    /// <summary>
    /// Plan P (revision C2): las piernas se mantienen de forma incremental. Colocar un miembro con 30 puntos
    /// propios bajo parent-001 por la derecha suma 30 puntos y 1 nodo a la pierna derecha de parent-001.
    /// </summary>
    [Fact]
    public async Task Handle_WhenSuccessful_AddsThePlacedPointsToTheParentLeg()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(BuildProfile("amb-001"));
        db.MemberStatistics.Add(new MLMConquerorGlobalEdition.Domain.Entities.Member.MemberStatisticEntity
            { MemberId = "amb-001", PersonalPoints = 30, CreatedBy = "seed", CreationDate = FixedNow });
        db.DualTeamTree.Add(BuildNode("parent-001", null, TreeSide.Left, "/parent-001/"));
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new AdminPlaceMemberCommand("amb-001", "parent-001", "Right"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var padre = db.DualTeamTree.First(d => d.MemberId == "parent-001");
        padre.RightLegPoints.Should().Be(30);
        padre.RightLegCount.Should().Be(1);
        padre.LeftLegPoints.Should().Be(0);
    }

    /// <summary>
    /// Decision de Nick (2026-10-18): una raiz provisional (tiene nodo y no tiene padre) se coloca con todo su
    /// equipo: la forma interna no cambia y su volumen acumulado sube a la linea nueva.
    /// </summary>
    [Fact]
    public async Task Handle_ProvisionalRootWithTeam_MovesTheWholeTeamAndItsVolume()
    {
        await using var db = InMemoryDbHelper.Create();
        var b = BuildProfile("amb-b"); b.SponsorMemberId = "parent-001";
        db.MemberProfiles.Add(b);
        db.DualTeamTree.AddRange(
            BuildNode("parent-001", null, TreeSide.Left, "/parent-001/"),
            BuildNode("amb-b", null, TreeSide.Left, "/amb-b/"),
            BuildNode("amb-c", "amb-b", TreeSide.Left, "/amb-b/amb-c/"),
            BuildNode("amb-d", "amb-c", TreeSide.Right, "/amb-b/amb-c/amb-d/"));
        await db.SaveChangesAsync();
        var nodoB = db.DualTeamTree.First(d => d.MemberId == "amb-b");
        nodoB.LeftLegPoints = 70; nodoB.LeftLegCount = 2;   // el volumen acumulado de su equipo
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new AdminPlaceMemberCommand("amb-b", "parent-001", "Left"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        db.DualTeamTree.First(d => d.MemberId == "amb-b").ParentMemberId.Should().Be("parent-001");
        db.DualTeamTree.First(d => d.MemberId == "amb-c").ParentMemberId.Should().Be("amb-b");
        db.DualTeamTree.First(d => d.MemberId == "amb-d").HierarchyPath.Should().Be("/parent-001/amb-b/amb-c/amb-d/");
        var padre = db.DualTeamTree.First(d => d.MemberId == "parent-001");
        padre.LeftLegPoints.Should().Be(70);
        padre.LeftLegCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_SystemRoot_IsNeverPlaced()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(BuildProfile("root-001"));   // sin patrocinador
        db.DualTeamTree.AddRange(
            BuildNode("parent-001", null, TreeSide.Left, "/parent-001/"),
            BuildNode("root-001", null, TreeSide.Left, "/root-001/"));
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new AdminPlaceMemberCommand("root-001", "parent-001", "Left"), CancellationToken.None);

        result.ErrorCode.Should().Be("ROOT_CANNOT_BE_PLACED");
    }
}
