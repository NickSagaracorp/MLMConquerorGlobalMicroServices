using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.Signups.Features.Placement.Commands.UnplaceMember;
using MLMConquerorGlobalEdition.Signups.Tests.Helpers;

namespace MLMConquerorGlobalEdition.Signups.Tests.Features.Placement;

public class UnplaceMemberHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> DateTimeAt(DateTime now)
    {
        var mock = new Mock<IDateTimeProvider>();
        mock.Setup(d => d.Now).Returns(now);
        return mock;
    }

    private static async Task SeedPlacedMember(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db,
        string memberId,
        DateTime firstPlacementDate,
        int unplacementCount = 0)
    {
        await db.DualTeamTree.AddAsync(new DualTeamEntity
        {
            MemberId = memberId,
            ParentMemberId = "AMB-000001",
            Side = TreeSide.Left,
            HierarchyPath = $"/AMB-000001/{memberId}",
            CreatedBy = "seed",
            LastUpdateDate = FixedNow
        });
        await db.PlacementLogs.AddAsync(new PlacementLog
        {
            MemberId = memberId,
            PlacedUnderMemberId = "AMB-000001",
            Side = TreeSide.Left,
            Action = "Placed",
            FirstPlacementDate = firstPlacementDate,
            UnplacementCount = unplacementCount,
            CreationDate = firstPlacementDate,
            CreatedBy = memberId
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WhenMemberHasNoActivePlacement_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new UnplaceMemberHandler(db, DateTimeAt(FixedNow).Object);

        var result = await handler.Handle(
            new UnplaceMemberCommand("AMB-NOT-PLACED", "AMB-000001"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PLACEMENT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenUnplacementCountIs2_ThrowsUnplacementLimitExceededException()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedPlacedMember(db, "AMB-000002", FixedNow.AddHours(-10), unplacementCount: 2);

        var handler = new UnplaceMemberHandler(db, DateTimeAt(FixedNow).Object);

        Func<Task> act = () => handler.Handle(
            new UnplaceMemberCommand("AMB-000002", "AMB-000001"), CancellationToken.None);

        await act.Should().ThrowAsync<UnplacementLimitExceededException>();
    }

    [Fact]
    public async Task Handle_WhenOver72HoursFromFirstPlacement_ThrowsUnplacementWindowExpiredException()
    {
        await using var db = InMemoryDbHelper.Create();
        var firstPlacementDate = FixedNow.AddHours(-73); // 73h ago — window expired
        await SeedPlacedMember(db, "AMB-000002", firstPlacementDate, unplacementCount: 0);

        var handler = new UnplaceMemberHandler(db, DateTimeAt(FixedNow).Object);

        Func<Task> act = () => handler.Handle(
            new UnplaceMemberCommand("AMB-000002", "AMB-000001"), CancellationToken.None);

        await act.Should().ThrowAsync<UnplacementWindowExpiredException>();
    }

    [Fact]
    public async Task Handle_WhenFirstUnplacement_SucceedsAndRemovesNodeCreatesLog()
    {
        await using var db = InMemoryDbHelper.Create();
        var firstPlacementDate = FixedNow.AddHours(-10); // within 72h window
        await SeedPlacedMember(db, "AMB-000002", firstPlacementDate, unplacementCount: 0);

        var handler = new UnplaceMemberHandler(db, DateTimeAt(FixedNow).Object);

        var result = await handler.Handle(
            new UnplaceMemberCommand("AMB-000002", "AMB-000001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // DualTeamEntity must be removed
        db.DualTeamTree.Any(n => n.MemberId == "AMB-000002").Should().BeFalse();

        // New PlacementLog with Action = "Unplaced" and incremented count
        var log = db.PlacementLogs
            .OrderByDescending(l => l.Id)
            .FirstOrDefault(l => l.MemberId == "AMB-000002");
        log.Should().NotBeNull();
        log!.Action.Should().Be("Unplaced");
        log.UnplacementCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenSecondUnplacement_SucceedsWithUnplacementCountOf2()
    {
        await using var db = InMemoryDbHelper.Create();
        var firstPlacementDate = FixedNow.AddHours(-10);
        await SeedPlacedMember(db, "AMB-000002", firstPlacementDate, unplacementCount: 1);

        var handler = new UnplaceMemberHandler(db, DateTimeAt(FixedNow).Object);

        var result = await handler.Handle(
            new UnplaceMemberCommand("AMB-000002", "AMB-000001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var log = db.PlacementLogs
            .OrderByDescending(l => l.Id)
            .FirstOrDefault(l => l.MemberId == "AMB-000002");
        log!.UnplacementCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenExactly72HoursFromFirstPlacement_Succeeds()
    {
        // Boundary: exactly 72h — TotalHours == 72 — NOT > 72, so allowed
        await using var db = InMemoryDbHelper.Create();
        var firstPlacementDate = FixedNow.AddHours(-72);
        await SeedPlacedMember(db, "AMB-000002", firstPlacementDate, unplacementCount: 0);

        var handler = new UnplaceMemberHandler(db, DateTimeAt(FixedNow).Object);

        var result = await handler.Handle(
            new UnplaceMemberCommand("AMB-000002", "AMB-000001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
