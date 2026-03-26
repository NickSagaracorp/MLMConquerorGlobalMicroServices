using MLMConquerorGlobalEdition.AdminAPI.DTOs.GhostPoints;
using MLMConquerorGlobalEdition.AdminAPI.Features.GhostPoints.CreateGhostPoint;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.GhostPoints;

public class CreateGhostPointHandlerTests
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

    [Fact]
    public async Task Handle_WhenValidRequest_CreatesGhostPointWithIsActiveTrue()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new CreateGhostPointHandler(db, CurrentUser().Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new CreateGhostPointCommand(new CreateGhostPointRequest
            {
                MemberId = "AMB-001",
                LegMemberId = "AMB-002",
                Points = 500,
                Side = TreeSide.Left,
                Notes = "Admin bonus"
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var entity = db.GhostPoints.Single();
        entity.IsActive.Should().BeTrue();
        entity.MemberId.Should().Be("AMB-001");
        entity.LegMemberId.Should().Be("AMB-002");
        entity.Points.Should().Be(500m);
        entity.Side.Should().Be(TreeSide.Left);
        entity.AdminNote.Should().Be("Admin bonus");
    }

    [Fact]
    public async Task Handle_WhenValidRequest_ReturnsDtoWithCorrectValues()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new CreateGhostPointHandler(db, CurrentUser().Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new CreateGhostPointCommand(new CreateGhostPointRequest
            {
                MemberId = "AMB-001",
                LegMemberId = "AMB-003",
                Points = 250,
                Side = TreeSide.Right
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MemberId.Should().Be("AMB-001");
        result.Value.LegMemberId.Should().Be("AMB-003");
        result.Value.Points.Should().Be(250m);
        result.Value.Side.Should().Be("Right");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenValidRequest_SetsCreatedByToCurrentUser()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new CreateGhostPointHandler(db, CurrentUser().Object, DateTimeProvider().Object);

        await handler.Handle(
            new CreateGhostPointCommand(new CreateGhostPointRequest
            {
                MemberId = "AMB-001",
                LegMemberId = "AMB-002",
                Points = 100,
                Side = TreeSide.Left
            }),
            CancellationToken.None);

        var entity = db.GhostPoints.Single();
        entity.CreatedBy.Should().Be("admin-001");
    }
}
