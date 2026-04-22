using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateMatchingBonus;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.CommissionEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.CommissionEngine.Tests.Features;

public class CalculateMatchingBonusHandlerTests
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

    private static MemberProfile BuildMember(string memberId, string? sponsor = null) => new()
    {
        MemberId        = memberId,
        FirstName       = "Test",
        LastName        = "Member",
        MemberType      = MemberType.Ambassador,
        Status          = MemberAccountStatus.Active,
        EnrollDate      = FixedNow.AddMonths(-3),
        Country         = "US",
        SponsorMemberId = sponsor,
        CreatedBy       = "seed",
        LastUpdateDate  = FixedNow
    };

    private static CommissionType BuildBaseType(int id = 1) => new()
    {
        Id               = id,
        Name             = "DailyResidual-Silver",
        IsActive         = true,
        ResidualBased    = true,
        ResidualOverCommissionType = 0,
        Percentage       = 0,
        Amount      = 50,
        PaymentDelayDays = 0,
        CreatedBy        = "seed",
        CreationDate     = FixedNow
    };

    private static CommissionType BuildMatchingType(int id = 2, int baseTypeId = 1,
        double matchingPct = 20) => new()
    {
        Id                         = id,
        Name                       = "MatchingBonus",
        IsActive                   = true,
        ResidualBased              = true,
        ResidualOverCommissionType = baseTypeId,
        ResidualPercentage         = matchingPct,
        Percentage                 = 0,
        PaymentDelayDays           = 0,
        CreatedBy                  = "seed",
        CreationDate               = FixedNow
    };

    private static CommissionEarning BuildBaseEarning(string beneficiary, DateTime periodDate,
        decimal amount = 50, int commTypeId = 1) => new()
    {
        BeneficiaryMemberId = beneficiary,
        CommissionTypeId    = commTypeId,
        Amount              = amount,
        Status              = CommissionEarningStatus.Pending,
        EarnedDate          = periodDate,
        PaymentDate         = periodDate.AddDays(7),
        PeriodDate          = periodDate,
        CreatedBy           = "seed",
        CreationDate        = periodDate,
        LastUpdateDate      = periodDate
    };

    [Fact]
    public async Task Handle_WhenNoMatchingTypes_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new CalculateMatchingBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateMatchingBonusCommand(null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_MATCHING_TYPES");
    }

    [Fact]
    public async Task Handle_WhenAlreadyRanForPeriod_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CommissionTypes.AddAsync(BuildMatchingType(id: 2, baseTypeId: 1));

        var periodDate = FixedNow.Date;
        await db.CommissionEarnings.AddAsync(new CommissionEarning
        {
            BeneficiaryMemberId = "AMB-001",
            CommissionTypeId    = 2,
            Amount              = 10,
            Status              = CommissionEarningStatus.Pending,
            EarnedDate          = periodDate,
            PaymentDate         = periodDate.AddDays(7),
            PeriodDate          = periodDate,
            CreatedBy           = "seed",
            CreationDate        = periodDate,
            LastUpdateDate      = periodDate
        });
        await db.SaveChangesAsync();

        var handler = new CalculateMatchingBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateMatchingBonusCommand(periodDate), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ALREADY_CALCULATED");
    }

    [Fact]
    public async Task Handle_WhenNoBaseEarningsForPeriod_SkipsWithSuccess()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CommissionTypes.AddAsync(BuildMatchingType(id: 2, baseTypeId: 1));
        // No base commission earnings seeded
        await db.SaveChangesAsync();

        var handler = new CalculateMatchingBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateMatchingBonusCommand(FixedNow.Date), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenDownlineHasBaseEarnings_CreatesSponsorMatchingBonus()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CommissionTypes.AddAsync(BuildMatchingType(id: 2, baseTypeId: 1, matchingPct: 20));
        await db.MemberProfiles.AddAsync(BuildMember("AMB-DOWNLINE", sponsor: "AMB-SPONSOR"));

        var periodDate = FixedNow.Date;
        // Downline earned $50 in base residual
        await db.CommissionEarnings.AddAsync(
            BuildBaseEarning("AMB-DOWNLINE", periodDate, amount: 50, commTypeId: 1));
        await db.SaveChangesAsync();

        var handler = new CalculateMatchingBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateMatchingBonusCommand(periodDate), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(1);
        // 20% of $50 = $10
        result.Value.TotalAmountCalculated.Should().Be(10);

        var earning = db.CommissionEarnings
            .Single(e => e.CommissionTypeId == 2);
        earning.BeneficiaryMemberId.Should().Be("AMB-SPONSOR");
        earning.Amount.Should().Be(10);
    }

    [Fact]
    public async Task Handle_AggregatesTotalEarningsPerDownlineMember()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CommissionTypes.AddAsync(BuildMatchingType(id: 2, baseTypeId: 1, matchingPct: 10));
        await db.MemberProfiles.AddAsync(BuildMember("AMB-DOWNLINE", sponsor: "AMB-SPONSOR"));

        var periodDate = FixedNow.Date;
        // Two base earnings for the same downline member in the same period
        await db.CommissionEarnings.AddRangeAsync(
            BuildBaseEarning("AMB-DOWNLINE", periodDate, amount: 50, commTypeId: 1),
            BuildBaseEarning("AMB-DOWNLINE", periodDate, amount: 30, commTypeId: 1));
        await db.SaveChangesAsync();

        var handler = new CalculateMatchingBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateMatchingBonusCommand(periodDate), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(1);
        // 10% of ($50 + $30) = $8
        result.Value.TotalAmountCalculated.Should().Be(8);
    }

    [Fact]
    public async Task Handle_WhenCalledTwice_IsIdempotent()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CommissionTypes.AddAsync(BuildMatchingType(id: 2, baseTypeId: 1, matchingPct: 20));
        await db.MemberProfiles.AddAsync(BuildMember("AMB-DOWNLINE", sponsor: "AMB-SPONSOR"));

        var periodDate = FixedNow.Date;
        await db.CommissionEarnings.AddAsync(
            BuildBaseEarning("AMB-DOWNLINE", periodDate, amount: 50, commTypeId: 1));
        await db.SaveChangesAsync();

        var handler = new CalculateMatchingBonusHandler(db, BuildClock().Object, BuildUser().Object);
        await handler.Handle(new CalculateMatchingBonusCommand(periodDate), CancellationToken.None);

        // Second call for the same period returns failure (already calculated)
        var result = await handler.Handle(
            new CalculateMatchingBonusCommand(periodDate), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ALREADY_CALCULATED");
        db.CommissionEarnings.Where(e => e.CommissionTypeId == 2).Should().HaveCount(1);
    }
}

