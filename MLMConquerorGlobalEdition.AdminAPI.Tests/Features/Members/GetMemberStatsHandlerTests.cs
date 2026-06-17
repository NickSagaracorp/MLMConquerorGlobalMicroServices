using MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMemberStats;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Members;

public class GetMemberStatsHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> Clock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static MemberProfile BuildMember(
        string memberId,
        MemberAccountStatus status = MemberAccountStatus.Active,
        DateTime? enrolledAt = null,
        bool isDeleted = false) => new()
    {
        MemberId       = memberId,
        FirstName      = "Test",
        LastName       = "User",
        Country        = "US",
        Status         = status,
        MemberType     = MemberType.Ambassador,
        EnrollDate     = enrolledAt ?? FixedNow.AddDays(-30),
        CreationDate   = enrolledAt ?? FixedNow.AddDays(-30),
        LastUpdateDate = FixedNow,
        CreatedBy      = "seed",
        IsDeleted      = isDeleted
    };

    [Fact]
    public async Task Handle_NoData_ReturnsZeros()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new GetMemberStatsHandler(db, Clock().Object, new NoOpCacheService());

        var result = await handler.Handle(new GetMemberStatsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalActive.Should().Be(0);
        result.Value.NewSignupsLast24Hours.Should().Be(0);
        result.Value.CancellationsLast24Hours.Should().Be(0);
        result.Value.PlacementsLast24Hours.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CountsActiveMembers_ExcludesInactive()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-001", MemberAccountStatus.Active),
            BuildMember("AMB-002", MemberAccountStatus.Active),
            BuildMember("AMB-003", MemberAccountStatus.Inactive),
            BuildMember("AMB-004", MemberAccountStatus.Suspended),
            BuildMember("AMB-005", MemberAccountStatus.Terminated),
            BuildMember("AMB-006", MemberAccountStatus.Pending),
            BuildMember("AMB-007", MemberAccountStatus.Active, isDeleted: true));
        await db.SaveChangesAsync();

        var handler = new GetMemberStatsHandler(db, Clock().Object, new NoOpCacheService());

        var result = await handler.Handle(new GetMemberStatsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // 2 active, non-deleted. The soft-deleted Active row is excluded.
        result.Value!.TotalActive.Should().Be(2);
    }

    [Fact]
    public async Task Handle_CountsSignupsWithinWindow_ExcludesOlderThan24h()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddRangeAsync(
            // Inside the 24h window
            BuildMember("AMB-001", enrolledAt: FixedNow.AddHours(-1)),
            BuildMember("AMB-002", enrolledAt: FixedNow.AddHours(-23)),
            // Outside the window (25h ago + 5 days ago)
            BuildMember("AMB-003", enrolledAt: FixedNow.AddHours(-25)),
            BuildMember("AMB-004", enrolledAt: FixedNow.AddDays(-5)),
            // Inside the window but soft-deleted — must not count
            BuildMember("AMB-005", enrolledAt: FixedNow.AddHours(-2), isDeleted: true));
        await db.SaveChangesAsync();

        var handler = new GetMemberStatsHandler(db, Clock().Object, new NoOpCacheService());

        var result = await handler.Handle(new GetMemberStatsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.NewSignupsLast24Hours.Should().Be(2);
    }

    [Fact]
    public async Task Handle_CountsCancellationsWithinWindow_ExcludesOlderThan24h()
    {
        await using var db = InMemoryDbHelper.Create();

        await db.MemberStatusHistories.AddRangeAsync(
            // Inside 24h window with cancellation-style new statuses
            new MemberStatusHistory
            {
                MemberId = "AMB-001", OldStatus = MemberAccountStatus.Active,
                NewStatus = MemberAccountStatus.Inactive,
                ChangedAt = FixedNow.AddHours(-1),
                CreationDate = FixedNow.AddHours(-1), CreatedBy = "admin"
            },
            new MemberStatusHistory
            {
                MemberId = "AMB-002", OldStatus = MemberAccountStatus.Active,
                NewStatus = MemberAccountStatus.Suspended,
                ChangedAt = FixedNow.AddHours(-12),
                CreationDate = FixedNow.AddHours(-12), CreatedBy = "admin"
            },
            new MemberStatusHistory
            {
                MemberId = "AMB-003", OldStatus = MemberAccountStatus.Active,
                NewStatus = MemberAccountStatus.Terminated,
                ChangedAt = FixedNow.AddHours(-23),
                CreationDate = FixedNow.AddHours(-23), CreatedBy = "admin"
            },
            // Inside window but reactivation (NewStatus = Active) — not a cancellation
            new MemberStatusHistory
            {
                MemberId = "AMB-004", OldStatus = MemberAccountStatus.Inactive,
                NewStatus = MemberAccountStatus.Active,
                ChangedAt = FixedNow.AddHours(-2),
                CreationDate = FixedNow.AddHours(-2), CreatedBy = "admin"
            },
            // Outside the window — must not count
            new MemberStatusHistory
            {
                MemberId = "AMB-005", OldStatus = MemberAccountStatus.Active,
                NewStatus = MemberAccountStatus.Inactive,
                ChangedAt = FixedNow.AddHours(-25),
                CreationDate = FixedNow.AddHours(-25), CreatedBy = "admin"
            });
        await db.SaveChangesAsync();

        var handler = new GetMemberStatsHandler(db, Clock().Object, new NoOpCacheService());

        var result = await handler.Handle(new GetMemberStatsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CancellationsLast24Hours.Should().Be(3);
    }

    [Fact]
    public async Task Handle_CountsPlacementsWithinWindow_ExcludesOlderThan24h()
    {
        await using var db = InMemoryDbHelper.Create();

        // Seed first (AuditInterceptor stamps CreationDate = real UtcNow), then
        // overwrite CreationDate to our deterministic test offsets and re-save.
        var rows = new[]
        {
            new DualTeamEntity { MemberId = "AMB-001", Side = TreeSide.Left,  HierarchyPath = "/AMB-001/", CreatedBy = "system" },
            new DualTeamEntity { MemberId = "AMB-002", Side = TreeSide.Right, HierarchyPath = "/AMB-002/", CreatedBy = "system" },
            new DualTeamEntity { MemberId = "AMB-003", Side = TreeSide.Left,  HierarchyPath = "/AMB-003/", CreatedBy = "system" },
            new DualTeamEntity { MemberId = "AMB-004", Side = TreeSide.Right, HierarchyPath = "/AMB-004/", CreatedBy = "system" }
        };
        await db.DualTeamTree.AddRangeAsync(rows);
        await db.SaveChangesAsync();

        // Rewrite the CreationDate windows — interceptor only re-stamps on Added,
        // so updating an already-tracked entity keeps our test timestamps.
        rows[0].CreationDate = FixedNow.AddHours(-1);   // inside window
        rows[1].CreationDate = FixedNow.AddHours(-12);  // inside window
        rows[2].CreationDate = FixedNow.AddHours(-25);  // outside window
        rows[3].CreationDate = FixedNow.AddDays(-7);    // outside window
        await db.SaveChangesAsync();

        var handler = new GetMemberStatsHandler(db, Clock().Object, new NoOpCacheService());

        var result = await handler.Handle(new GetMemberStatsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PlacementsLast24Hours.Should().Be(2);
    }
}
