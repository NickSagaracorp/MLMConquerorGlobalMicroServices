using MLMConquerorGlobalEdition.AdminAPI.Features.Placement.AdminRemovePlacement;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Placement;

public class AdminRemovePlacementHandlerTests
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

    private static DualTeamEntity BuildNode(string memberId, string? parentId = null,
        TreeSide side = TreeSide.Left, string path = "") => new()
    {
        MemberId       = memberId,
        ParentMemberId = parentId,
        Side           = side,
        HierarchyPath  = string.IsNullOrEmpty(path) ? $"/{memberId}/" : path,
        CreationDate   = FixedNow,
        LastUpdateDate = FixedNow,
        CreatedBy      = "seed",
        LastUpdateBy   = "seed"
    };

    private static AdminRemovePlacementHandler CreateHandler(
        Repository.Context.AppDbContext db) =>
        new(db, CurrentUser().Object, Clock().Object);

    [Fact]
    public async Task Handle_WhenMemberNotInTree_ReturnsNotPlaced()
    {
        await using var db = InMemoryDbHelper.Create();

        var result = await CreateHandler(db).Handle(
            new AdminRemovePlacementCommand("not-placed-member"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NOT_PLACED");
    }

    [Fact]
    public async Task Handle_WhenMemberIsPlaced_RemovesNodeFromTree()
    {
        await using var db = InMemoryDbHelper.Create();
        db.DualTeamTree.Add(BuildNode("amb-001", "parent-001", TreeSide.Left, "/parent-001/amb-001/"));
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new AdminRemovePlacementCommand("amb-001"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        db.DualTeamTree.Any(d => d.MemberId == "amb-001").Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenMemberIsPlaced_LogsRemovalAction()
    {
        await using var db = InMemoryDbHelper.Create();
        db.DualTeamTree.Add(BuildNode("amb-001", "parent-001", TreeSide.Left, "/parent-001/amb-001/"));
        await db.SaveChangesAsync();

        await CreateHandler(db).Handle(
            new AdminRemovePlacementCommand("amb-001"),
            CancellationToken.None);

        db.PlacementLogs.Any(p => p.MemberId == "amb-001" && p.Action == "Removed")
          .Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenMemberHasDirectChildren_ChildrenBecomeRoots()
    {
        await using var db = InMemoryDbHelper.Create();
        // Tree: parent-001 → amb-001 (left) → child-left (left)
        //                              → child-right (right)
        db.DualTeamTree.AddRange(
            BuildNode("amb-001",     "parent-001", TreeSide.Left,  "/parent-001/amb-001/"),
            BuildNode("child-left",  "amb-001",    TreeSide.Left,  "/parent-001/amb-001/child-left/"),
            BuildNode("child-right", "amb-001",    TreeSide.Right, "/parent-001/amb-001/child-right/")
        );
        await db.SaveChangesAsync();

        await CreateHandler(db).Handle(
            new AdminRemovePlacementCommand("amb-001"),
            CancellationToken.None);

        var leftChild  = db.DualTeamTree.First(d => d.MemberId == "child-left");
        var rightChild = db.DualTeamTree.First(d => d.MemberId == "child-right");

        leftChild.ParentMemberId.Should().BeNull();
        leftChild.HierarchyPath.Should().Be("/child-left/");

        rightChild.ParentMemberId.Should().BeNull();
        rightChild.HierarchyPath.Should().Be("/child-right/");
    }

    [Fact]
    public async Task Handle_WhenSuccess_ReturnsSuccessMessage()
    {
        await using var db = InMemoryDbHelper.Create();
        db.DualTeamTree.Add(BuildNode("amb-001"));
        await db.SaveChangesAsync();

        var result = await CreateHandler(db).Handle(
            new AdminRemovePlacementCommand("amb-001"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("exitosamente");
    }
}
