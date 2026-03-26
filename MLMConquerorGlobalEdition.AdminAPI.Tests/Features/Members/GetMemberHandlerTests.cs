using MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMember;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Members;

public class GetMemberHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static MemberProfile BuildMember(string memberId) => new()
    {
        MemberId = memberId,
        FirstName = "Alice",
        LastName = "Smith",
        Country = "US",
        Status = MemberAccountStatus.Active,
        MemberType = MemberType.Ambassador,
        EnrollDate = FixedNow.AddDays(-30),
        CreationDate = FixedNow.AddDays(-30),
        LastUpdateDate = FixedNow,
        CreatedBy = "seed"
    };

    [Fact]
    public async Task Handle_WhenMemberNotFound_ReturnsMemberNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new GetMemberHandler(db);

        var result = await handler.Handle(new GetMemberQuery("AMB-999"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MEMBER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenMemberFoundWithNoStats_ReturnsDtoWithZeroStats()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        await db.SaveChangesAsync();

        var handler = new GetMemberHandler(db);
        var result = await handler.Handle(new GetMemberQuery("AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MemberId.Should().Be("AMB-001");
        result.Value.DualTeamPoints.Should().Be(0);
        result.Value.EnrollmentPoints.Should().Be(0);
        result.Value.DualTeamSize.Should().Be(0);
        result.Value.EnrollmentTeamSize.Should().Be(0);
        result.Value.CurrentMonthIncome.Should().Be(0);
        result.Value.CurrentYearIncome.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenMemberFoundWithStats_ReturnsDtoWithStats()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        await db.MemberStatistics.AddAsync(new MemberStatisticEntity
        {
            MemberId = "AMB-001",
            DualTeamPoints = 100,
            EnrollmentPoints = 50,
            DualTeamSize = 10,
            EnrollmentTeamSize = 5,
            CurrentMonthIncomeGrowth = 200m,
            CurrentYearIncomeGrowth = 1500m,
            CreationDate = FixedNow,
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        var handler = new GetMemberHandler(db);
        var result = await handler.Handle(new GetMemberQuery("AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DualTeamPoints.Should().Be(100);
        result.Value.EnrollmentPoints.Should().Be(50);
        result.Value.DualTeamSize.Should().Be(10);
        result.Value.EnrollmentTeamSize.Should().Be(5);
        result.Value.CurrentMonthIncome.Should().Be(200m);
        result.Value.CurrentYearIncome.Should().Be(1500m);
    }

    [Fact]
    public async Task Handle_WhenMemberFound_MapsMemberFieldsCorrectly()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001"));
        await db.SaveChangesAsync();

        var handler = new GetMemberHandler(db);
        var result = await handler.Handle(new GetMemberQuery("AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FirstName.Should().Be("Alice");
        result.Value.LastName.Should().Be("Smith");
        result.Value.Status.Should().Be("Active");
        result.Value.MemberType.Should().Be("Ambassador");
    }
}
