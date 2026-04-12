using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Features.Placement.Queries.ValidatePlacement;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Placement;

public class ValidatePlacementHandlerTests
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
        LastName = "User",
        MemberType = MemberType.Ambassador,
        EnrollDate = enrollDate,
        Country = "US",
        CreatedBy = "seed",
        LastUpdateDate = FixedNow
    };

    [Fact]
    public async Task Handle_WhenMemberNotFound_ReturnsFailureWithMemberNotFoundCode()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new ValidatePlacementHandler(db, DateTimeAt(FixedNow).Object);

        var result = await handler.Handle(
            new ValidatePlacementQuery("NON-EXISTENT", "AMB-000001", "Left"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenPlacementWindowExpired_ReturnsFailureWithWindowExpiredCode()
    {
        await using var db = InMemoryDbHelper.Create();
        var enrollDate = FixedNow.AddDays(-31);
        await db.MemberProfiles.AddAsync(BuildMember("AMB-000002", enrollDate));
        await db.SaveChangesAsync();

        var handler = new ValidatePlacementHandler(db, DateTimeAt(FixedNow).Object);

        var result = await handler.Handle(
            new ValidatePlacementQuery("AMB-000002", "AMB-000001", "Left"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PLACEMENT_WINDOW_EXPIRED");
    }

    [Fact]
    public async Task Handle_WhenParentMemberNotFound_ReturnsFailureWithParentNotFoundCode()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-000002", FixedNow.AddDays(-5)));
        await db.SaveChangesAsync();

        var handler = new ValidatePlacementHandler(db, DateTimeAt(FixedNow).Object);

        var result = await handler.Handle(
            new ValidatePlacementQuery("AMB-000002", "NON-EXISTENT-PARENT", "Left"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PARENT_MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenSideIsInvalid_ReturnsFailureWithInvalidSideCode()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-000002", FixedNow.AddDays(-5)),
            BuildMember("AMB-000001", FixedNow.AddDays(-60))
        );
        await db.SaveChangesAsync();

        var handler = new ValidatePlacementHandler(db, DateTimeAt(FixedNow).Object);

        var result = await handler.Handle(
            new ValidatePlacementQuery("AMB-000002", "AMB-000001", "Center"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_SIDE");
    }

    [Fact]
    public async Task Handle_WhenPositionOccupied_ReturnsFailureWithPositionOccupiedCode()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-000002", FixedNow.AddDays(-5)),
            BuildMember("AMB-000001", FixedNow.AddDays(-60))
        );
        await db.DualTeamTree.AddAsync(new DualTeamEntity
        {
            MemberId = "AMB-000003",
            ParentMemberId = "AMB-000001",
            Side = TreeSide.Right,
            HierarchyPath = "/AMB-000001/AMB-000003",
            CreatedBy = "seed",
            LastUpdateDate = FixedNow
        });
        await db.SaveChangesAsync();

        var handler = new ValidatePlacementHandler(db, DateTimeAt(FixedNow).Object);

        var result = await handler.Handle(
            new ValidatePlacementQuery("AMB-000002", "AMB-000001", "Right"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("POSITION_OCCUPIED");
    }

    [Fact]
    public async Task Handle_WhenAllValidationsPass_ReturnsSuccess()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-000002", FixedNow.AddDays(-5)),
            BuildMember("AMB-000001", FixedNow.AddDays(-60))
        );
        await db.SaveChangesAsync();

        var handler = new ValidatePlacementHandler(db, DateTimeAt(FixedNow).Object);

        var result = await handler.Handle(
            new ValidatePlacementQuery("AMB-000002", "AMB-000001", "Left"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SideIsCaseInsensitive_AcceptsLowerCase()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-000002", FixedNow.AddDays(-5)),
            BuildMember("AMB-000001", FixedNow.AddDays(-60))
        );
        await db.SaveChangesAsync();

        var handler = new ValidatePlacementHandler(db, DateTimeAt(FixedNow).Object);

        var result = await handler.Handle(
            new ValidatePlacementQuery("AMB-000002", "AMB-000001", "right"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
