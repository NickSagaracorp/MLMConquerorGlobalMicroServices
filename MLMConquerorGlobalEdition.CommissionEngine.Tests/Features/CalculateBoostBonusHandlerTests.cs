using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateBoostBonus;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.CommissionEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.CommissionEngine.Tests.Features;

public class CalculateBoostBonusHandlerTests
{
    // Sunday at the start of a week — the handler defaults to "this week's Sunday"
    private static readonly DateTime FixedNow = new(2026, 3, 22, 12, 0, 0, DateTimeKind.Utc); // Sunday

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

    private static MemberProfile BuildAmbassador(string memberId,
        DateTime? enrollDate = null, string? sponsor = null) => new()
    {
        MemberId        = memberId,
        FirstName       = "Test",
        LastName        = "Member",
        MemberType      = MemberType.Ambassador,
        Status          = MemberAccountStatus.Active,
        EnrollDate      = enrollDate ?? FixedNow.AddMonths(-6),
        Country         = "US",
        SponsorMemberId = sponsor,
        CreatedBy       = "seed",
        LastUpdateDate  = FixedNow
    };

    private static DualTeamEntity BuildDualNode(string memberId,
        int leftPoints = 0, int rightPoints = 0) => new()
    {
        MemberId        = memberId,
        ParentMemberId  = null,
        Side            = TreeSide.Left,
        HierarchyPath   = $"/{memberId}",
        LeftLegPoints   = leftPoints,
        RightLegPoints  = rightPoints,
        CreatedBy       = "seed",
        LastUpdateDate  = FixedNow
    };

    private static CommissionType BuildBoostType(int id, int teamPoints = 0,
        int newMembers = 2, decimal? fixedAmount = 600) => new()
    {
        Id               = id,
        Name             = $"Boost-{id}",
        IsActive         = true,
        IsPaidOnSignup   = false,
        ResidualBased    = false,
        IsSponsorBonus   = false,
        TriggerOrder     = 1,
        TeamPoints       = teamPoints,
        NewMembers       = newMembers,
        FixedAmount      = fixedAmount,
        Percentage       = 0,
        PaymentDelayDays = 0,
        CreatedBy        = "seed",
        CreationDate     = FixedNow
    };

    [Fact]
    public async Task Handle_WhenNoBoostTypes_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new CalculateBoostBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateBoostBonusCommand(null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_BOOST_TYPES");
    }

    [Fact]
    public async Task Handle_WhenAlreadyRanForWeek_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var boostType = BuildBoostType(id: 1, newMembers: 2);
        await db.CommissionTypes.AddAsync(boostType);

        var weekStart = FixedNow.Date.AddDays(-(int)FixedNow.DayOfWeek);
        await db.CommissionEarnings.AddAsync(new CommissionEarning
        {
            BeneficiaryMemberId = "AMB-001",
            CommissionTypeId    = 1,
            Amount              = 600,
            Status              = CommissionEarningStatus.Pending,
            EarnedDate          = weekStart,
            PaymentDate         = weekStart.AddDays(7),
            PeriodDate          = weekStart,
            CreatedBy           = "seed",
            CreationDate        = weekStart,
            LastUpdateDate      = weekStart
        });
        await db.SaveChangesAsync();

        var handler = new CalculateBoostBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateBoostBonusCommand(weekStart), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ALREADY_CALCULATED");
    }

    [Fact]
    public async Task Handle_WhenAmbassadorMeetsNewMemberThreshold_CreatesEarning()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CommissionTypes.AddAsync(BuildBoostType(id: 1, newMembers: 2, fixedAmount: 600));

        var weekStart = FixedNow.Date;
        var sponsor = BuildAmbassador("AMB-SPONSOR");
        await db.MemberProfiles.AddAsync(sponsor);

        // Two new members enrolled this week under the sponsor
        await db.MemberProfiles.AddAsync(BuildAmbassador("AMB-NEW1",
            enrollDate: weekStart.AddHours(2), sponsor: "AMB-SPONSOR"));
        await db.MemberProfiles.AddAsync(BuildAmbassador("AMB-NEW2",
            enrollDate: weekStart.AddHours(4), sponsor: "AMB-SPONSOR"));
        await db.DualTeamTree.AddAsync(BuildDualNode("AMB-SPONSOR", leftPoints: 0, rightPoints: 0));
        await db.SaveChangesAsync();

        var handler = new CalculateBoostBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateBoostBonusCommand(weekStart), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(1);
        result.Value.TotalAmountCalculated.Should().Be(600);
    }

    [Fact]
    public async Task Handle_WhenAmbassadorBelowThreshold_SkipsEarning()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CommissionTypes.AddAsync(BuildBoostType(id: 1, newMembers: 3, fixedAmount: 600));

        var weekStart = FixedNow.Date;
        await db.MemberProfiles.AddAsync(BuildAmbassador("AMB-SPONSOR"));
        // Only 1 new member this week — threshold is 3
        await db.MemberProfiles.AddAsync(BuildAmbassador("AMB-NEW1",
            enrollDate: weekStart.AddHours(2), sponsor: "AMB-SPONSOR"));
        await db.DualTeamTree.AddAsync(BuildDualNode("AMB-SPONSOR", leftPoints: 0, rightPoints: 0));
        await db.SaveChangesAsync();

        var handler = new CalculateBoostBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateBoostBonusCommand(weekStart), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTiered_PaysBestQualifyingTierOnly()
    {
        await using var db = InMemoryDbHelper.Create();
        // Gold = 2 new members/$600; Platinum = 4 new members/$1200
        await db.CommissionTypes.AddRangeAsync(
            BuildBoostType(id: 1, newMembers: 2, fixedAmount: 600),
            BuildBoostType(id: 2, newMembers: 4, fixedAmount: 1200));

        var weekStart = FixedNow.Date;
        await db.MemberProfiles.AddAsync(BuildAmbassador("AMB-SPONSOR"));
        // 4 new members → qualifies for Platinum tier
        for (int i = 1; i <= 4; i++)
            await db.MemberProfiles.AddAsync(BuildAmbassador($"AMB-NEW{i}",
                enrollDate: weekStart.AddHours(i), sponsor: "AMB-SPONSOR"));
        await db.DualTeamTree.AddAsync(BuildDualNode("AMB-SPONSOR"));
        await db.SaveChangesAsync();

        var handler = new CalculateBoostBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateBoostBonusCommand(weekStart), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(1);
        result.Value.TotalAmountCalculated.Should().Be(1200);
    }
}
