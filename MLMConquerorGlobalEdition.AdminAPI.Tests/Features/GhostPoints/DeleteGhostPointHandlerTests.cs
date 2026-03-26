using MLMConquerorGlobalEdition.AdminAPI.Features.GhostPoints.DeleteGhostPoint;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.GhostPoints;

public class DeleteGhostPointHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<ICurrentUserService> CurrentUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("admin-001");
        return m;
    }

    private static Mock<IDateTimeProvider> DateTimeProvider()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static GhostPointEntity BuildGhostPoint() => new()
    {
        MemberId = "AMB-001",
        LegMemberId = "AMB-002",
        Points = 300m,
        Side = TreeSide.Left,
        IsActive = true,
        CreationDate = FixedNow.AddDays(-5),
        LastUpdateDate = FixedNow.AddDays(-5),
        CreatedBy = "seed"
    };

    [Fact]
    public async Task Handle_WhenGhostPointNotFound_ReturnsGhostPointNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new DeleteGhostPointHandler(db, CurrentUser().Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new DeleteGhostPointCommand("nonexistent-id"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("GHOST_POINT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenGhostPointExists_SetsIsActiveToFalse()
    {
        await using var db = InMemoryDbHelper.Create();
        var entity = BuildGhostPoint();
        await db.GhostPoints.AddAsync(entity);
        await db.SaveChangesAsync();

        var handler = new DeleteGhostPointHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        var result = await handler.Handle(
            new DeleteGhostPointCommand(entity.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        var updated = db.GhostPoints.Single();
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenDeleted_DoesNotRemoveEntityFromDatabase()
    {
        await using var db = InMemoryDbHelper.Create();
        var entity = BuildGhostPoint();
        await db.GhostPoints.AddAsync(entity);
        await db.SaveChangesAsync();

        var handler = new DeleteGhostPointHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        await handler.Handle(new DeleteGhostPointCommand(entity.Id), CancellationToken.None);

        db.GhostPoints.Count().Should().Be(1);
    }
}
