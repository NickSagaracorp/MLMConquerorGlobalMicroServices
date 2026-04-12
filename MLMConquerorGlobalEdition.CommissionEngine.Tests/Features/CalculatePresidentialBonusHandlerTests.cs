using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculatePresidentialBonus;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.CommissionEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.CommissionEngine.Tests.Features;

public class CalculatePresidentialBonusHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 1, 4, 0, 0, DateTimeKind.Utc);

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

    private static CommissionType BuildPresidentialType(int id, int lifeTimeRank = 5,
        decimal percentage = 1) => new()
    {
        Id               = id,
        Name             = $"Presidential-{id}",
        IsActive         = true,
        IsPaidOnSignup   = false,
        LifeTimeRank     = lifeTimeRank,
        Percentage       = percentage,
        PaymentDelayDays = 0,
        CreatedBy        = "seed",
        CreationDate     = FixedNow
    };

    private static RankDefinition BuildRankDefinition(int id, int sortOrder) => new()
    {
        Id           = id,
        Name         = $"Rank-{sortOrder}",
        SortOrder    = sortOrder,
        Status       = RankDefinitionStatus.Active,
        CreatedBy    = "seed",
        CreationDate = FixedNow
    };

    private static MemberRankHistory BuildRankHistory(string memberId, int rankId) => new()
    {
        MemberId         = memberId,
        RankDefinitionId = rankId,
        AchievedAt       = FixedNow.AddMonths(-2),
        CreatedBy        = "seed",
        CreationDate     = FixedNow.AddMonths(-2),
        LastUpdateDate   = FixedNow.AddMonths(-2)
    };

    private static Orders BuildOrder(string id, decimal total = 1000) => new()
    {
        Id             = id,
        MemberId       = "AMB-ANY",
        TotalAmount    = total,
        Status         = OrderStatus.Completed,
        OrderDate      = FixedNow,
        CreatedBy      = "seed",
        CreationDate   = FixedNow,
        LastUpdateDate = FixedNow
    };

    [Fact]
    public async Task Handle_WhenNoPresidentialTypes_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new CalculatePresidentialBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculatePresidentialBonusCommand(null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_PRESIDENTIAL_TYPES");
    }

    [Fact]
    public async Task Handle_WhenAlreadyRanForMonth_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CommissionTypes.AddAsync(BuildPresidentialType(id: 1));

        var monthStart = new DateTime(2026, 4, 1);
        await db.CommissionEarnings.AddAsync(new CommissionEarning
        {
            BeneficiaryMemberId = "AMB-001",
            CommissionTypeId    = 1,
            Amount              = 100,
            Status              = CommissionEarningStatus.Pending,
            EarnedDate          = monthStart,
            PaymentDate         = monthStart.AddDays(7),
            PeriodDate          = monthStart,
            CreatedBy           = "seed",
            CreationDate        = monthStart,
            LastUpdateDate      = monthStart
        });
        await db.SaveChangesAsync();

        var handler = new CalculatePresidentialBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculatePresidentialBonusCommand(monthStart), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ALREADY_CALCULATED");
    }

    [Fact]
    public async Task Handle_WhenNoMembersQualify_SkipsWithSuccess()
    {
        await using var db = InMemoryDbHelper.Create();
        // Requires lifetime rank sort order >= 5, but no members have it
        await db.CommissionTypes.AddAsync(BuildPresidentialType(id: 1, lifeTimeRank: 5));
        await db.RankDefinitions.AddAsync(BuildRankDefinition(1, sortOrder: 3));
        await db.MemberRankHistories.AddAsync(BuildRankHistory("AMB-001", rankId: 1));
        await db.Orders.AddAsync(BuildOrder("ORD-001", total: 10000));
        await db.SaveChangesAsync();

        var handler = new CalculatePresidentialBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculatePresidentialBonusCommand(FixedNow), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenMembersQualify_SplitsPoolEvenlyAmongThem()
    {
        await using var db = InMemoryDbHelper.Create();
        // 1% of monthly volume split among qualifying members
        await db.CommissionTypes.AddAsync(BuildPresidentialType(id: 1, lifeTimeRank: 5, percentage: 1));
        await db.RankDefinitions.AddAsync(BuildRankDefinition(1, sortOrder: 6));
        await db.MemberRankHistories.AddRangeAsync(
            BuildRankHistory("AMB-001", rankId: 1),
            BuildRankHistory("AMB-002", rankId: 1));
        // $10,000 monthly volume → 1% pool = $100 → $50 per member
        await db.Orders.AddAsync(BuildOrder("ORD-001", total: 10000));
        await db.SaveChangesAsync();

        var handler = new CalculatePresidentialBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculatePresidentialBonusCommand(FixedNow), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(2);
        result.Value.TotalAmountCalculated.Should().Be(100);

        var earnings = db.CommissionEarnings.ToList();
        earnings.Should().HaveCount(2);
        earnings.Should().AllSatisfy(e => e.Amount.Should().Be(50));
    }
}
