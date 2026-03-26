using MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetGrowthDashboard;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Dashboards;

public class GetGrowthDashboardHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> DateTimeProvider()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static MemberProfile BuildMember(string memberId, DateTime enrollDate) => new()
    {
        MemberId = memberId,
        FirstName = "Test",
        LastName = "User",
        Country = "US",
        Status = MemberAccountStatus.Active,
        MemberType = MemberType.Ambassador,
        EnrollDate = enrollDate,
        CreationDate = enrollDate,
        LastUpdateDate = enrollDate,
        CreatedBy = "seed"
    };

    [Fact]
    public async Task Handle_WhenNoMembers_ReturnsZeroCounts()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new GetGrowthDashboardHandler(db, DateTimeProvider().Object);

        var result = await handler.Handle(new GetGrowthDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalMembers.Should().Be(0);
        result.Value.NewMembersThisMonth.Should().Be(0);
        result.Value.NewMembersThisWeek.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CountsTotalMembersRegardlessOfEnrollDate()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-001", FixedNow.AddDays(-60)),
            BuildMember("AMB-002", FixedNow.AddDays(-5)),
            BuildMember("AMB-003", FixedNow.AddDays(-1)));
        await db.SaveChangesAsync();

        var handler = new GetGrowthDashboardHandler(db, DateTimeProvider().Object);
        var result = await handler.Handle(new GetGrowthDashboardQuery(), CancellationToken.None);

        result.Value!.TotalMembers.Should().Be(3);
    }

    [Fact]
    public async Task Handle_CountsNewMembersThisMonthFromStartOfMonth()
    {
        await using var db = InMemoryDbHelper.Create();
        var startOfMonth = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-001", startOfMonth),              // start of month — included
            BuildMember("AMB-002", FixedNow.AddDays(-5)),      // this month
            BuildMember("AMB-003", new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc))); // last month — excluded
        await db.SaveChangesAsync();

        var handler = new GetGrowthDashboardHandler(db, DateTimeProvider().Object);
        var result = await handler.Handle(new GetGrowthDashboardQuery(), CancellationToken.None);

        result.Value!.NewMembersThisMonth.Should().Be(2);
    }

    [Fact]
    public async Task Handle_CountsNewMembersThisWeekFromSevenDaysAgo()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-001", FixedNow.AddDays(-6)),   // 6 days ago — within week
            BuildMember("AMB-002", FixedNow.AddDays(-7)),   // exactly 7 days ago — within week
            BuildMember("AMB-003", FixedNow.AddDays(-8)),   // 8 days ago — excluded
            BuildMember("AMB-004", FixedNow.AddDays(-60))); // old member — excluded
        await db.SaveChangesAsync();

        var handler = new GetGrowthDashboardHandler(db, DateTimeProvider().Object);
        var result = await handler.Handle(new GetGrowthDashboardQuery(), CancellationToken.None);

        result.Value!.NewMembersThisWeek.Should().Be(2);
    }
}
