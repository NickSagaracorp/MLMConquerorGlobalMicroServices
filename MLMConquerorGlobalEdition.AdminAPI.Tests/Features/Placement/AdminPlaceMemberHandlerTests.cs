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
        parent.Depth.Should().Be(0);

        var placed = db.DualTeamTree.FirstOrDefault(d => d.MemberId == "amb-001");
        placed.Should().NotBeNull();
        placed!.ParentMemberId.Should().Be("nonexistent-parent");
        placed.Side.Should().Be(TreeSide.Left);
        placed.Depth.Should().Be(1);
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
    public async Task Handle_WhenValidPlacement_SetsParentSideAndDepth()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(BuildProfile("amb-001"));
        db.DualTeamTree.Add(BuildNode("parent-001", null, TreeSide.Left, "/parent-001/"));
        await db.SaveChangesAsync();

        await CreateHandler(db).Handle(
            new AdminPlaceMemberCommand("amb-001", "parent-001", "Left"),
            CancellationToken.None);

        var node = db.DualTeamTree.First(d => d.MemberId == "amb-001");
        node.ParentMemberId.Should().Be("parent-001");
        node.Side.Should().Be(TreeSide.Left);
        node.Depth.Should().Be(1);
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
        db.DualTeamTree.First(d => d.MemberId == "amb-d").ParentMemberId.Should().Be("amb-c");
        db.DualTeamTree.First(d => d.MemberId == "amb-d").Depth.Should().Be(3, "parent-001 -> amb-b -> amb-c -> amb-d");
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

    /// <summary>
    /// Revision C1 del Plan P: el admin retira a X, que tiene equipo (C con 10 puntos y D con 5 bajo C); sus hijos esperan
    /// flotando. Al volver a colocar a X, el volumen de su equipo tiene que sumarse a X y a su linea nueva. Antes, al
    /// recolgar a C se restaba por la linea nueva (X ya existia) lo mismo que luego se sumaba: neto 0, y X pasaba por
    /// piernas negativas.
    /// </summary>
    [Fact]
    public async Task Handle_ReplacingARemovedMemberWithTeam_AddsTheTeamVolume()
    {
        await using var db = InMemoryDbHelper.Create();
        foreach (var id in new[] { "amb-x", "amb-c", "amb-d" }) db.MemberProfiles.Add(BuildProfile(id));
        db.MemberStatistics.AddRange(
            new MLMConquerorGlobalEdition.Domain.Entities.Member.MemberStatisticEntity { MemberId = "amb-x", PersonalPoints = 1, CreatedBy = "seed" },
            new MLMConquerorGlobalEdition.Domain.Entities.Member.MemberStatisticEntity { MemberId = "amb-c", PersonalPoints = 10, CreatedBy = "seed" },
            new MLMConquerorGlobalEdition.Domain.Entities.Member.MemberStatisticEntity { MemberId = "amb-d", PersonalPoints = 5, CreatedBy = "seed" });
        db.DualTeamTree.AddRange(
            BuildNode("parent-001", null, TreeSide.Left, "/parent-001/"),
            BuildNode("parent-002", null, TreeSide.Left, "/parent-002/"),
            BuildNode("amb-x", "parent-001", TreeSide.Left, "/parent-001/amb-x/"),
            BuildNode("amb-c", "amb-x", TreeSide.Left, "/parent-001/amb-x/amb-c/"),
            BuildNode("amb-d", "amb-c", TreeSide.Left, "/parent-001/amb-x/amb-c/amb-d/"));
        await db.SaveChangesAsync();
        foreach (var (id, l, n) in new[] { ("parent-001", 16m, 3), ("amb-x", 15m, 2), ("amb-c", 5m, 1) })
        {
            var nodo = db.DualTeamTree.First(d => d.MemberId == id);
            nodo.LeftLegPoints = l; nodo.LeftLegCount = n;
        }
        await db.SaveChangesAsync();

        (await new MLMConquerorGlobalEdition.AdminAPI.Features.Placement.AdminRemovePlacement.AdminRemovePlacementHandler(
            db, CurrentUser().Object, Clock().Object).Handle(new MLMConquerorGlobalEdition.AdminAPI.Features.Placement.AdminRemovePlacement.AdminRemovePlacementCommand("amb-x"), CancellationToken.None))
            .IsSuccess.Should().BeTrue();
        db.DualTeamTree.First(d => d.MemberId == "parent-001").LeftLegPoints.Should().Be(0);

        var result = await CreateHandler(db).Handle(new AdminPlaceMemberCommand("amb-x", "parent-002", "Right"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var x = db.DualTeamTree.First(d => d.MemberId == "amb-x");
        (x.LeftLegPoints, x.LeftLegCount).Should().Be((15m, 2), "el equipo de X vuelve con X");
        var p2 = db.DualTeamTree.First(d => d.MemberId == "parent-002");
        (p2.RightLegPoints, p2.RightLegCount).Should().Be((16m, 3), "X y su equipo suben a la linea nueva");
        db.DualTeamTree.Where(d => d.MemberId.StartsWith("amb") || d.MemberId.StartsWith("parent")).ToList()
            .Should().OnlyContain(d => d.LeftLegPoints >= 0 && d.RightLegPoints >= 0 && d.LeftLegCount >= 0 && d.RightLegCount >= 0);
    }
}
