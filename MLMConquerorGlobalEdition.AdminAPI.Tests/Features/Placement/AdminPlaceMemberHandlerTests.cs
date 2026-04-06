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
    public async Task Handle_WhenTargetParentNotInDualTree_ReturnsTargetNotFound()
    {
        await using var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(BuildProfile("amb-001"));
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new AdminPlaceMemberCommand("amb-001", "nonexistent-parent", "Left"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TARGET_NOT_FOUND");
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
}
