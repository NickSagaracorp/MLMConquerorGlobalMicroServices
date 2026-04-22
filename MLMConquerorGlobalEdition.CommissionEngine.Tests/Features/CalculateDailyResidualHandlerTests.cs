using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateDailyResidual;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.CommissionEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.CommissionEngine.Tests.Features;

public class CalculateDailyResidualHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> BuildClock(DateTime? at = null)
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(at ?? FixedNow);
        return m;
    }

    private static Mock<ICurrentUserService> BuildUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("system");
        return m;
    }

    private static MemberProfile BuildAmbassador(string memberId) => new()
    {
        MemberId       = memberId,
        FirstName      = "Test",
        LastName       = "Member",
        MemberType     = MemberType.Ambassador,
        Status         = MemberAccountStatus.Active,
        EnrollDate     = FixedNow.AddMonths(-3),
        Country        = "US",
        CreatedBy      = "seed",
        LastUpdateDate = FixedNow
    };

    private static MemberStatisticEntity BuildStats(string memberId, int dualTeamPoints = 0,
        int enrollmentPoints = 0) => new()
    {
        MemberId         = memberId,
        DualTeamPoints   = dualTeamPoints,
        EnrollmentPoints = enrollmentPoints,
        CreatedBy        = "seed",
        CreationDate     = FixedNow
    };

    private static CommissionType BuildResidualType(int id, int teamPoints = 100,
        decimal? Amount = 50, bool isEnrollmentBased = false) => new()
    {
        Id               = id,
        Name             = $"DTR-{teamPoints}",
        IsActive         = true,
        ResidualBased    = true,
        IsPaidOnSignup   = false,
        TeamPoints       = teamPoints,
        Amount      = Amount,
        Percentage       = 0,
        IsEnrollmentBased = isEnrollmentBased,
        PaymentDelayDays  = 0,
        CreatedBy         = "seed",
        CreationDate      = FixedNow
    };

    [Fact]
    public async Task Handle_WhenNoResidualTypes_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new CalculateDailyResidualHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateDailyResidualCommand(null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_RESIDUAL_TYPES");
    }

    [Fact]
    public async Task Handle_WhenAlreadyRanForPeriod_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var residualType = BuildResidualType(id: 1);
        await db.CommissionTypes.AddAsync(residualType);

        var periodDate = FixedNow.Date;
        await db.CommissionEarnings.AddAsync(new CommissionEarning
        {
            BeneficiaryMemberId = "AMB-001",
            CommissionTypeId    = 1,
            Amount              = 50,
            Status              = CommissionEarningStatus.Pending,
            EarnedDate          = periodDate,
            PaymentDate         = periodDate.AddDays(7),
            PeriodDate          = periodDate,
            CreatedBy           = "seed",
            CreationDate        = periodDate,
            LastUpdateDate      = periodDate
        });
        await db.SaveChangesAsync();

        var handler = new CalculateDailyResidualHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateDailyResidualCommand(periodDate), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ALREADY_CALCULATED");
    }

    [Fact]
    public async Task Handle_WhenAmbassadorMeetsThreshold_CreatesEarning()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CommissionTypes.AddAsync(BuildResidualType(id: 1, teamPoints: 100, Amount: 50));
        await db.MemberProfiles.AddAsync(BuildAmbassador("AMB-001"));
        await db.MemberStatistics.AddAsync(BuildStats("AMB-001", dualTeamPoints: 200));
        await db.SaveChangesAsync();

        var handler = new CalculateDailyResidualHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateDailyResidualCommand(FixedNow.Date), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(1);
        result.Value.TotalAmountCalculated.Should().Be(50);
    }

    [Fact]
    public async Task Handle_WhenAmbassadorBelowThreshold_SkipsEarning()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CommissionTypes.AddAsync(BuildResidualType(id: 1, teamPoints: 500, Amount: 50));
        await db.MemberProfiles.AddAsync(BuildAmbassador("AMB-001"));
        await db.MemberStatistics.AddAsync(BuildStats("AMB-001", dualTeamPoints: 100));
        await db.SaveChangesAsync();

        var handler = new CalculateDailyResidualHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateDailyResidualCommand(FixedNow.Date), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTiered_PaysBestQualifyingTierOnly()
    {
        await using var db = InMemoryDbHelper.Create();
        // Two tiers: Silver=100pts/$30, Gold=300pts/$60. Ambassador has 350 points → pays Gold.
        await db.CommissionTypes.AddRangeAsync(
            BuildResidualType(id: 1, teamPoints: 100, Amount: 30),
            BuildResidualType(id: 2, teamPoints: 300, Amount: 60));
        await db.MemberProfiles.AddAsync(BuildAmbassador("AMB-001"));
        await db.MemberStatistics.AddAsync(BuildStats("AMB-001", dualTeamPoints: 350));
        await db.SaveChangesAsync();

        var handler = new CalculateDailyResidualHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateDailyResidualCommand(FixedNow.Date), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(1);
        // Should receive Gold ($60), not Silver ($30) — handler picks highest qualifying tier
        result.Value.TotalAmountCalculated.Should().Be(60);
    }

    [Fact]
    public async Task Handle_ExcludesNonAmbassadorMembers()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CommissionTypes.AddAsync(BuildResidualType(id: 1, teamPoints: 100, Amount: 50));
        var externalMember = BuildAmbassador("EXT-001");
        externalMember.MemberType = MemberType.ExternalMember;
        await db.MemberProfiles.AddAsync(externalMember);
        await db.MemberStatistics.AddAsync(BuildStats("EXT-001", dualTeamPoints: 500));
        await db.SaveChangesAsync();

        var handler = new CalculateDailyResidualHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateDailyResidualCommand(FixedNow.Date), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(0);
    }
}

