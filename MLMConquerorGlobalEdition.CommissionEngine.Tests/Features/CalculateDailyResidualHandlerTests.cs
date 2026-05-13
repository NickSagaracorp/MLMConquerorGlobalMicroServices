using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateDailyResidual;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.CommissionEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
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
        int enrollmentPoints = 0, int personalPoints = 0) => new()
    {
        MemberId         = memberId,
        DualTeamPoints   = dualTeamPoints,
        EnrollmentPoints = enrollmentPoints,
        PersonalPoints   = personalPoints,
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

    private static RankDefinition BuildRank(int id, string name, int sortOrder) => new()
    {
        Id           = id,
        Name         = name,
        SortOrder    = sortOrder,
        Status       = RankDefinitionStatus.Active,
        CreatedBy    = "seed",
        CreationDate = FixedNow
    };

    private static MemberRankHistory BuildRankHistory(string memberId, int rankId, int sortOrder) => new()
    {
        MemberId        = memberId,
        RankDefinitionId = rankId,
        AchievedAt      = FixedNow.AddDays(-10),
        IsDeleted       = false,
        CreatedBy       = "seed",
        CreationDate    = FixedNow,
        LastUpdateDate  = FixedNow,
        RankDefinition  = BuildRank(rankId, $"Rank-{sortOrder}", sortOrder)
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
        // Guard now checks DailyResidualEarning (new table) — seed a row there, not CommissionEarning.
        await using var db = InMemoryDbHelper.Create();
        var residualType = BuildResidualType(id: 1);
        await db.CommissionTypes.AddAsync(residualType);

        var periodDate = FixedNow.Date;
        await db.DailyResidualEarnings.AddAsync(new Domain.Entities.Commission.DailyResidualEarning
        {
            BeneficiaryMemberId = "AMB-001",
            Amount              = 50,
            EarnedDate          = periodDate,
            Status              = CommissionEarningStatus.Pending,
            CreatedBy           = "seed",
            CreationDate        = periodDate
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

    // ── Snapshot field tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenMemberHasNoRank_SnapshotCurrentRankIdIsNull()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CommissionTypes.AddAsync(BuildResidualType(id: 1, teamPoints: 100, Amount: 50));
        await db.MemberProfiles.AddAsync(BuildAmbassador("AMB-001"));
        await db.MemberStatistics.AddAsync(BuildStats("AMB-001", dualTeamPoints: 200, personalPoints: 10));
        // No MemberRankHistory rows for AMB-001
        await db.SaveChangesAsync();

        var handler = new CalculateDailyResidualHandler(db, BuildClock().Object, BuildUser().Object);
        await handler.Handle(new CalculateDailyResidualCommand(FixedNow.Date), CancellationToken.None);

        var earned = db.DailyResidualEarnings.Single();
        earned.CurrentRankId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenMemberHasRank_SnapshotCurrentRankIdMatchesHighestSortOrderRank()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CommissionTypes.AddAsync(BuildResidualType(id: 1, teamPoints: 100, Amount: 50));
        await db.MemberProfiles.AddAsync(BuildAmbassador("AMB-001"));
        await db.MemberStatistics.AddAsync(BuildStats("AMB-001", dualTeamPoints: 200));

        // Two rank histories — the one with higher SortOrder (rank Id=2, SortOrder=20) should be picked
        await db.MemberRankHistories.AddRangeAsync(
            BuildRankHistory("AMB-001", rankId: 1, sortOrder: 10),
            BuildRankHistory("AMB-001", rankId: 2, sortOrder: 20));
        await db.SaveChangesAsync();

        var handler = new CalculateDailyResidualHandler(db, BuildClock().Object, BuildUser().Object);
        await handler.Handle(new CalculateDailyResidualCommand(FixedNow.Date), CancellationToken.None);

        var earned = db.DailyResidualEarnings.Single();
        earned.CurrentRankId.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenDualTeamTier_SnapshotEligibleDualTeamPointsSetAndEnrollmentPointsZero()
    {
        await using var db = InMemoryDbHelper.Create();
        // DT-based type (IsEnrollmentBased = false)
        await db.CommissionTypes.AddAsync(BuildResidualType(id: 1, teamPoints: 100, Amount: 50, isEnrollmentBased: false));
        await db.MemberProfiles.AddAsync(BuildAmbassador("AMB-001"));
        await db.MemberStatistics.AddAsync(BuildStats("AMB-001", dualTeamPoints: 300, enrollmentPoints: 50, personalPoints: 5));
        await db.SaveChangesAsync();

        var handler = new CalculateDailyResidualHandler(db, BuildClock().Object, BuildUser().Object);
        await handler.Handle(new CalculateDailyResidualCommand(FixedNow.Date), CancellationToken.None);

        var earned = db.DailyResidualEarnings.Single();
        // DT axis was used: EligibleDualTeamPoints = 300, EligibleEnrollmentTeamPoints = 0
        earned.EligibleDualTeamPoints.Should().Be(300);
        earned.EligibleEnrollmentTeamPoints.Should().Be(0);
        earned.PersonalPoints.Should().Be(5);
    }

    [Fact]
    public async Task Handle_WhenEnrollmentTeamTier_SnapshotEligibleEnrollmentPointsSetAndDualTeamPointsZero()
    {
        await using var db = InMemoryDbHelper.Create();
        // ET-based type (IsEnrollmentBased = true)
        await db.CommissionTypes.AddAsync(BuildResidualType(id: 1, teamPoints: 50, Amount: 40, isEnrollmentBased: true));
        await db.MemberProfiles.AddAsync(BuildAmbassador("AMB-001"));
        await db.MemberStatistics.AddAsync(BuildStats("AMB-001", dualTeamPoints: 100, enrollmentPoints: 200, personalPoints: 7));
        await db.SaveChangesAsync();

        var handler = new CalculateDailyResidualHandler(db, BuildClock().Object, BuildUser().Object);
        await handler.Handle(new CalculateDailyResidualCommand(FixedNow.Date), CancellationToken.None);

        var earned = db.DailyResidualEarnings.Single();
        // ET axis was used: EligibleEnrollmentTeamPoints = 200, EligibleDualTeamPoints = 0
        earned.EligibleEnrollmentTeamPoints.Should().Be(200);
        earned.EligibleDualTeamPoints.Should().Be(0);
        earned.PersonalPoints.Should().Be(7);
    }

    [Fact]
    public async Task Handle_PersonalPoints_SnapshotMatchesMemberStatisticsValue()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CommissionTypes.AddAsync(BuildResidualType(id: 1, teamPoints: 100, Amount: 50));
        await db.MemberProfiles.AddAsync(BuildAmbassador("AMB-001"));
        // personalPoints = 42 — verify it is captured exactly
        await db.MemberStatistics.AddAsync(BuildStats("AMB-001", dualTeamPoints: 200, personalPoints: 42));
        await db.SaveChangesAsync();

        var handler = new CalculateDailyResidualHandler(db, BuildClock().Object, BuildUser().Object);
        await handler.Handle(new CalculateDailyResidualCommand(FixedNow.Date), CancellationToken.None);

        var earned = db.DailyResidualEarnings.Single();
        earned.PersonalPoints.Should().Be(42);
    }

    [Fact]
    public async Task Handle_WhenMultipleMembers_EachEarningCarriesCorrectSnapshots()
    {
        await using var db = InMemoryDbHelper.Create();
        // DT-based type
        await db.CommissionTypes.AddAsync(BuildResidualType(id: 1, teamPoints: 100, Amount: 50, isEnrollmentBased: false));

        await db.MemberProfiles.AddRangeAsync(
            BuildAmbassador("AMB-001"),
            BuildAmbassador("AMB-002"));

        await db.MemberStatistics.AddRangeAsync(
            BuildStats("AMB-001", dualTeamPoints: 200, personalPoints: 10),
            BuildStats("AMB-002", dualTeamPoints: 300, personalPoints: 20));

        // AMB-001 has rank 5; AMB-002 has no rank
        await db.MemberRankHistories.AddAsync(BuildRankHistory("AMB-001", rankId: 5, sortOrder: 50));
        await db.SaveChangesAsync();

        var handler = new CalculateDailyResidualHandler(db, BuildClock().Object, BuildUser().Object);
        await handler.Handle(new CalculateDailyResidualCommand(FixedNow.Date), CancellationToken.None);

        var earnings = db.DailyResidualEarnings.OrderBy(e => e.BeneficiaryMemberId).ToList();
        earnings.Should().HaveCount(2);

        var amb001 = earnings.First(e => e.BeneficiaryMemberId == "AMB-001");
        amb001.CurrentRankId.Should().Be(5);
        amb001.EligibleDualTeamPoints.Should().Be(200);
        amb001.PersonalPoints.Should().Be(10);

        var amb002 = earnings.First(e => e.BeneficiaryMemberId == "AMB-002");
        amb002.CurrentRankId.Should().BeNull();
        amb002.EligibleDualTeamPoints.Should().Be(300);
        amb002.PersonalPoints.Should().Be(20);
    }

    [Fact]
    public async Task Handle_WhenSoftDeletedRankHistory_NotIncludedInCurrentRank()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CommissionTypes.AddAsync(BuildResidualType(id: 1, teamPoints: 100, Amount: 50));
        await db.MemberProfiles.AddAsync(BuildAmbassador("AMB-001"));
        await db.MemberStatistics.AddAsync(BuildStats("AMB-001", dualTeamPoints: 200));

        // Only soft-deleted rank history — should be excluded, CurrentRankId = null
        var deletedHistory = BuildRankHistory("AMB-001", rankId: 3, sortOrder: 30);
        deletedHistory.IsDeleted = true;
        await db.MemberRankHistories.AddAsync(deletedHistory);
        await db.SaveChangesAsync();

        var handler = new CalculateDailyResidualHandler(db, BuildClock().Object, BuildUser().Object);
        await handler.Handle(new CalculateDailyResidualCommand(FixedNow.Date), CancellationToken.None);

        var earned = db.DailyResidualEarnings.Single();
        // The soft-deleted rank history is filtered out — no current rank
        earned.CurrentRankId.Should().BeNull();
    }
}
