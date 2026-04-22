using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateBoostBonus;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.CommissionEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.CommissionEngine.Tests.Features;

public class CalculateBoostBonusHandlerTests
{
    // Monday — comp plan week starts on Monday
    private static readonly DateTime FixedNow = new(2026, 3, 23, 12, 0, 0, DateTimeKind.Utc);

    // weekStart = Monday Mar 23 2026
    private static DateTime WeekStart => FixedNow.Date.AddDays(-(((int)FixedNow.DayOfWeek + 6) % 7));

    private static Mock<IDateTimeProvider> Clock(DateTime? at = null)
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(at ?? FixedNow);
        return m;
    }

    private static Mock<ICurrentUserService> User()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("system");
        return m;
    }

    // ── Builders ──────────────────────────────────────────────────────────────

    private static CommissionType BoostType(int id, int newMembers, decimal amount,
        int currentRankSortOrder = 3, double branchCap = 0.5) => new()
    {
        Id               = id,
        CommissionCategoryId = 5,
        Name             = $"BoostTier-{id}",
        IsActive         = true,
        IsPaidOnSignup   = false,
        ResidualBased    = false,
        IsSponsorBonus   = false,
        TriggerOrder     = 1,
        NewMembers       = newMembers,
        Amount           = amount,
        Percentage       = 0,
        PaymentDelayDays = 0,
        LifeTimeRank     = currentRankSortOrder,
        MaxEnrollmentTeamPointsPerBranch = branchCap,
        CreatedBy        = "seed",
        CreationDate     = FixedNow
    };

    private static MemberProfile Ambassador(string memberId, DateTime? enrollDate = null) => new()
    {
        MemberId       = memberId,
        FirstName      = "Test",
        LastName       = "Member",
        MemberType     = MemberType.Ambassador,
        Status         = MemberAccountStatus.Active,
        EnrollDate     = enrollDate ?? FixedNow.AddMonths(-6),
        Country        = "US",
        CreatedBy      = "seed",
        LastUpdateDate = FixedNow
    };

    private static RankDefinition Rank(int id, string name, int sortOrder) => new()
    {
        Id        = id,
        Name      = name,
        SortOrder = sortOrder,
        CreatedBy = "seed",
        CreationDate = FixedNow
    };

    private static MemberRankHistory RankHistory(string memberId, int rankDefinitionId,
        DateTime? achievedAt = null) => new()
    {
        MemberId         = memberId,
        RankDefinitionId = rankDefinitionId,
        AchievedAt       = achievedAt ?? FixedNow.AddMonths(-3),
        CreatedBy        = "seed",
        CreationDate     = FixedNow,
        LastUpdateDate   = FixedNow
    };

    private static GenealogyEntity GenealogyNode(string memberId, string hierarchyPath) => new()
    {
        MemberId       = memberId,
        HierarchyPath  = hierarchyPath,
        Level          = hierarchyPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length,
        CreatedBy      = "seed",
        CreationDate   = FixedNow,
        LastUpdateDate = FixedNow
    };

    /// <summary>
    /// Creates the full set of records required for a new Elite/Turbo enrollment
    /// to be visible to the Boost Bonus handler.
    /// </summary>
    private static async Task SeedNewEliteTurboMember(
        Repository.Context.AppDbContext db,
        string newMemberId,
        DateTime enrollDate,
        string hierarchyPath,
        int membershipLevelId = 3) // 3=Elite, 4=Turbo
    {
        var productId = $"PROD-{membershipLevelId}";

        // FindAsync checks the change tracker first, then DB — safe for multi-call seeding
        if (await db.Products.FindAsync(productId) == null)
        {
            db.Products.Add(new Product
            {
                Id                = productId,
                Name              = $"Level-{membershipLevelId}",
                Description       = string.Empty,
                ImageUrl          = string.Empty,
                MonthlyFee        = 99,
                SetupFee          = 0,
                IsActive          = true,
                MembershipLevelId = membershipLevelId,
                CreatedBy         = "seed",
                CreationDate      = FixedNow,
                LastUpdateDate    = FixedNow
            });
        }

        db.MemberProfiles.Add(Ambassador(newMemberId, enrollDate));

        var orderId = $"ORD-{newMemberId}";
        db.Orders.Add(new Orders
        {
            Id             = orderId,
            MemberId       = newMemberId,
            Status         = OrderStatus.Completed,
            TotalAmount    = 99,
            OrderDate      = enrollDate,
            CreatedBy      = "seed",
            CreationDate   = enrollDate,
            LastUpdateDate = enrollDate
        });
        db.OrderDetails.Add(new OrderDetail
        {
            OrderId    = orderId,
            ProductId  = productId,
            Quantity   = 1,
            UnitPrice  = 99,
            CreatedBy  = "seed",
            CreationDate = enrollDate
        });

        db.GenealogyTree.Add(GenealogyNode(newMemberId, hierarchyPath));
    }

    // ── Guard tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNoBoostTypes_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new CalculateBoostBonusHandler(db, Clock().Object, User().Object);

        var result = await handler.Handle(new CalculateBoostBonusCommand(null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_BOOST_TYPES");
    }

    [Fact]
    public async Task Handle_WhenAlreadyRanForWeek_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(BoostType(id: 1, newMembers: 6, amount: 600));
        var weekStart = WeekStart;
        db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = "AMB-001",
            CommissionTypeId    = 1,
            Amount              = 600,
            Status              = CommissionEarningStatus.Pending,
            EarnedDate          = weekStart,
            PaymentDate         = weekStart.AddDays(30),
            PeriodDate          = weekStart,
            CreatedBy           = "seed",
            CreationDate        = weekStart,
            LastUpdateDate      = weekStart
        });
        await db.SaveChangesAsync();

        var handler = new CalculateBoostBonusHandler(db, Clock().Object, User().Object);
        var result = await handler.Handle(new CalculateBoostBonusCommand(weekStart), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ALREADY_CALCULATED");
    }

    [Fact]
    public async Task Handle_WhenNoEliteTurboEnrollments_ReturnsSuccessWithZeroRecords()
    {
        await using var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(BoostType(id: 1, newMembers: 6, amount: 600));
        await db.SaveChangesAsync();

        var handler = new CalculateBoostBonusHandler(db, Clock().Object, User().Object);
        var result = await handler.Handle(new CalculateBoostBonusCommand(WeekStart), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(0);
    }

    // ── Week boundary ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WeekStartsOnMonday_NotSunday()
    {
        // FixedNow is Monday Mar 23 2026 → weekStart should be Mar 23, not Mar 22 (Sunday)
        var monday = new DateTime(2026, 3, 23, 12, 0, 0, DateTimeKind.Utc);
        var expected = monday.Date; // Monday

        await using var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(BoostType(id: 1, newMembers: 6, amount: 600));

        // Seed a rank and ambassador so we have something to run against
        var rank = Rank(1, "Gold", 3);
        db.RankDefinitions.Add(rank);
        db.MemberProfiles.Add(Ambassador("AMB-UPLINE"));
        db.MemberRankHistories.Add(RankHistory("AMB-UPLINE", 1));
        db.GenealogyTree.Add(GenealogyNode("AMB-UPLINE", "/AMB-UPLINE/"));
        await db.SaveChangesAsync();

        var handler = new CalculateBoostBonusHandler(db, Clock(monday).Object, User().Object);
        var result = await handler.Handle(new CalculateBoostBonusCommand(null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PeriodDate.Should().Be(expected);
    }

    // ── Enrollment tree credit logic ──────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNewMemberHasNoGoldPlatinumUpline_SkipsNewMember()
    {
        await using var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(BoostType(id: 1, newMembers: 1, amount: 600, currentRankSortOrder: 3));

        // Bronze upline (SortOrder=1) — does NOT qualify as Gold/Platinum
        db.RankDefinitions.Add(Rank(1, "Bronze", 1));
        db.MemberProfiles.Add(Ambassador("AMB-BRONZE"));
        db.MemberRankHistories.Add(RankHistory("AMB-BRONZE", 1));
        db.GenealogyTree.Add(GenealogyNode("AMB-BRONZE", "/AMB-BRONZE/"));

        await db.SaveChangesAsync();

        var weekStart = WeekStart;
        await SeedNewEliteTurboMember(db, "NEW-001", weekStart.AddHours(2),
            "/AMB-BRONZE/NEW-001/");
        await db.SaveChangesAsync();

        var handler = new CalculateBoostBonusHandler(db, Clock().Object, User().Object);
        var result = await handler.Handle(new CalculateBoostBonusCommand(weekStart), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(0);
        result.Value.SkippedReasons.Should().Contain(s =>
            s.Contains("NEW-001") && s.Contains("no Gold/Platinum rank upline"));
    }

    [Fact]
    public async Task Handle_WhenGoldUplineBetweenPlatinumAndNewMember_CreditsGoldNotPlatinum()
    {
        // Hierarchy: AMB-PLATINUM → AMB-GOLD → NEW-001..NEW-006
        // AMB-GOLD is the FIRST Gold/Platinum upline → gets all 6 credits, not AMB-PLATINUM.
        await using var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(BoostType(id: 1, newMembers: 6, amount: 600, currentRankSortOrder: 3));

        db.RankDefinitions.AddRange(
            Rank(1, "Gold",     3),
            Rank(2, "Platinum", 5));

        db.MemberProfiles.AddRange(Ambassador("AMB-PLATINUM"), Ambassador("AMB-GOLD"));
        db.MemberRankHistories.AddRange(
            RankHistory("AMB-PLATINUM", 2),
            RankHistory("AMB-GOLD",     1));
        db.GenealogyTree.AddRange(
            GenealogyNode("AMB-PLATINUM", "/AMB-PLATINUM/"),
            GenealogyNode("AMB-GOLD",     "/AMB-PLATINUM/AMB-GOLD/"));

        await db.SaveChangesAsync();

        var weekStart = WeekStart;
        for (int i = 1; i <= 6; i++)
            await SeedNewEliteTurboMember(db, $"NEW-{i:D3}", weekStart.AddHours(i),
                $"/AMB-PLATINUM/AMB-GOLD/NEW-{i:D3}/");
        await db.SaveChangesAsync();

        var handler = new CalculateBoostBonusHandler(db, Clock().Object, User().Object);
        var result = await handler.Handle(new CalculateBoostBonusCommand(weekStart), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(2); // 50% + 50%
        var earnings = await db.CommissionEarnings.ToListAsync();
        earnings.Should().AllSatisfy(e => e.BeneficiaryMemberId.Should().Be("AMB-GOLD"));
        earnings.Should().NotContain(e => e.BeneficiaryMemberId == "AMB-PLATINUM");
    }

    [Fact]
    public async Task Handle_WhenNewMemberIsDirectChildOfGoldUpline_CreditsGoldUpline()
    {
        await using var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(BoostType(id: 1, newMembers: 6, amount: 600, currentRankSortOrder: 3));

        db.RankDefinitions.Add(Rank(1, "Gold", 3));
        db.MemberProfiles.Add(Ambassador("AMB-GOLD"));
        db.MemberRankHistories.Add(RankHistory("AMB-GOLD", 1));
        db.GenealogyTree.Add(GenealogyNode("AMB-GOLD", "/AMB-GOLD/"));

        await db.SaveChangesAsync();

        var weekStart = WeekStart;
        for (int i = 1; i <= 6; i++)
            await SeedNewEliteTurboMember(db, $"NEW-{i:D3}", weekStart.AddHours(i),
                $"/AMB-GOLD/NEW-{i:D3}/");
        await db.SaveChangesAsync();

        var handler = new CalculateBoostBonusHandler(db, Clock().Object, User().Object);
        var result = await handler.Handle(new CalculateBoostBonusCommand(weekStart), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(2); // 50% + 50%
        result.Value.TotalAmountCalculated.Should().Be(600);
    }

    // ── 50% leg cap ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenOneLegExceedsHalfThreshold_EffectiveCountIsCapped()
    {
        // Gold threshold = 6. Cap per leg = 3.
        // Leg-A has 5 new members, Leg-B has 1 → effective = min(5,3) + min(1,3) = 4 → does NOT qualify.
        await using var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(BoostType(id: 1, newMembers: 6, amount: 600, branchCap: 0.5));

        db.RankDefinitions.Add(Rank(1, "Gold", 3));
        db.MemberProfiles.AddRange(Ambassador("AMB-GOLD"), Ambassador("LEG-A"), Ambassador("LEG-B"));
        db.MemberRankHistories.Add(RankHistory("AMB-GOLD", 1));
        db.GenealogyTree.AddRange(
            GenealogyNode("AMB-GOLD", "/AMB-GOLD/"),
            GenealogyNode("LEG-A",    "/AMB-GOLD/LEG-A/"),
            GenealogyNode("LEG-B",    "/AMB-GOLD/LEG-B/"));

        await db.SaveChangesAsync();

        var weekStart = WeekStart;
        // 5 under Leg-A
        for (int i = 1; i <= 5; i++)
            await SeedNewEliteTurboMember(db, $"NEW-A{i}", weekStart.AddHours(i),
                $"/AMB-GOLD/LEG-A/NEW-A{i}/");
        // 1 under Leg-B
        await SeedNewEliteTurboMember(db, "NEW-B1", weekStart.AddHours(6),
            "/AMB-GOLD/LEG-B/NEW-B1/");
        await db.SaveChangesAsync();

        var handler = new CalculateBoostBonusHandler(db, Clock().Object, User().Object);
        var result = await handler.Handle(new CalculateBoostBonusCommand(weekStart), CancellationToken.None);

        // effective = 3 + 1 = 4 < 6 → no qualification
        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenLegsAreBalanced_CapHasNoEffect()
    {
        // Gold threshold = 6. Cap per leg = 3.
        // Leg-A = 3, Leg-B = 3 → effective = 6 → qualifies.
        await using var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(BoostType(id: 1, newMembers: 6, amount: 600, branchCap: 0.5));

        db.RankDefinitions.Add(Rank(1, "Gold", 3));
        db.MemberProfiles.AddRange(Ambassador("AMB-GOLD"), Ambassador("LEG-A"), Ambassador("LEG-B"));
        db.MemberRankHistories.Add(RankHistory("AMB-GOLD", 1));
        db.GenealogyTree.AddRange(
            GenealogyNode("AMB-GOLD", "/AMB-GOLD/"),
            GenealogyNode("LEG-A",    "/AMB-GOLD/LEG-A/"),
            GenealogyNode("LEG-B",    "/AMB-GOLD/LEG-B/"));

        await db.SaveChangesAsync();

        var weekStart = WeekStart;
        for (int i = 1; i <= 3; i++)
            await SeedNewEliteTurboMember(db, $"NEW-A{i}", weekStart.AddHours(i),
                $"/AMB-GOLD/LEG-A/NEW-A{i}/");
        for (int i = 1; i <= 3; i++)
            await SeedNewEliteTurboMember(db, $"NEW-B{i}", weekStart.AddHours(3 + i),
                $"/AMB-GOLD/LEG-B/NEW-B{i}/");
        await db.SaveChangesAsync();

        var handler = new CalculateBoostBonusHandler(db, Clock().Object, User().Object);
        var result = await handler.Handle(new CalculateBoostBonusCommand(weekStart), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(2);
        result.Value.TotalAmountCalculated.Should().Be(600);
    }

    // ── Tier selection ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenPlatinumAmbIsFirstUpline_EarnsOnlyPlatinumNotGold()
    {
        // Gold type (CurrentRank=3, threshold=6, $600)
        // Platinum type (CurrentRank=5, threshold=12, $1,200)
        // AMB-PLAT rank 5 is the first ancestor for all 12 new members.
        // Walk finds AMB-PLAT (rank 5 >= 3). Highest qualifying tier = Platinum (rank 5 >= 5).
        // AMB-PLAT is credited in Platinum only — never in Gold.
        // Platinum: 12 balanced credits → qualifies → $1,200 (2 earnings).
        // Gold pool stays empty → $0.
        await using var db = InMemoryDbHelper.Create();
        db.CommissionTypes.AddRange(
            BoostType(id: 1, newMembers:  6, amount:   600, currentRankSortOrder: 3),
            BoostType(id: 2, newMembers: 12, amount: 1200, currentRankSortOrder: 5));

        db.RankDefinitions.AddRange(
            Rank(1, "Gold",     3),
            Rank(2, "Platinum", 5));
        db.MemberProfiles.AddRange(Ambassador("AMB-PLAT"), Ambassador("LEG-A"), Ambassador("LEG-B"));
        db.MemberRankHistories.Add(RankHistory("AMB-PLAT", 2)); // Platinum SortOrder=5
        db.GenealogyTree.AddRange(
            GenealogyNode("AMB-PLAT", "/AMB-PLAT/"),
            GenealogyNode("LEG-A",    "/AMB-PLAT/LEG-A/"),
            GenealogyNode("LEG-B",    "/AMB-PLAT/LEG-B/"));

        await db.SaveChangesAsync();

        var weekStart = WeekStart;
        for (int i = 1; i <= 6; i++)
            await SeedNewEliteTurboMember(db, $"NEW-A{i}", weekStart.AddHours(i),
                $"/AMB-PLAT/LEG-A/NEW-A{i}/");
        for (int i = 1; i <= 6; i++)
            await SeedNewEliteTurboMember(db, $"NEW-B{i}", weekStart.AddHours(6 + i),
                $"/AMB-PLAT/LEG-B/NEW-B{i}/");
        await db.SaveChangesAsync();

        var handler = new CalculateBoostBonusHandler(db, Clock().Object, User().Object);
        var result = await handler.Handle(new CalculateBoostBonusCommand(weekStart), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(2);           // Platinum only
        result.Value.TotalAmountCalculated.Should().Be(1200);

        var earnings = await db.CommissionEarnings.ToListAsync();
        earnings.Should().AllSatisfy(e =>
        {
            e.BeneficiaryMemberId.Should().Be("AMB-PLAT");
            e.CommissionTypeId.Should().Be(2); // Platinum
        });
    }

    [Fact]
    public async Task Handle_WhenGoldAmbBelowPlatinum_EachTierCreditsSeparateUpline()
    {
        // AMB-PLATINUM
        //   ├── AMB-GOLD-L (Gold) ← 6 new members
        //   └── AMB-GOLD-R (Gold) ← 6 new members
        //
        // Gold walk: first Gold+ ancestor per new member = AMB-GOLD-L or AMB-GOLD-R.
        //   AMB-GOLD-L: 6 credits → qualifies Gold → $600 (2 earnings).
        //   AMB-GOLD-R: 6 credits → qualifies Gold → $600 (2 earnings).
        //
        // Platinum walk: starts at each Gold ambassador (rank 3 < 5), continues up → AMB-PLATINUM.
        //   legId=AMB-GOLD-L (6 credits) + legId=AMB-GOLD-R (6 credits) = 12 balanced.
        //   Cap per Platinum leg = floor(12*0.5)=6 → effective 6+6=12 >= 12 ✓ → $1,200 (2 earnings).
        //
        // Total: 6 earnings, $2,400.
        await using var db = InMemoryDbHelper.Create();
        db.CommissionTypes.AddRange(
            BoostType(id: 1, newMembers:  6, amount:   600, currentRankSortOrder: 3),
            BoostType(id: 2, newMembers: 12, amount: 1200, currentRankSortOrder: 5));

        db.RankDefinitions.AddRange(
            Rank(1, "Gold",     3),
            Rank(2, "Platinum", 5));
        db.MemberProfiles.AddRange(
            Ambassador("AMB-PLATINUM"),
            Ambassador("AMB-GOLD-L"),
            Ambassador("AMB-GOLD-R"));
        db.MemberRankHistories.AddRange(
            RankHistory("AMB-PLATINUM", 2), // Platinum SortOrder=5
            RankHistory("AMB-GOLD-L",   1), // Gold SortOrder=3
            RankHistory("AMB-GOLD-R",   1));
        db.GenealogyTree.AddRange(
            GenealogyNode("AMB-PLATINUM", "/AMB-PLATINUM/"),
            GenealogyNode("AMB-GOLD-L",   "/AMB-PLATINUM/AMB-GOLD-L/"),
            GenealogyNode("AMB-GOLD-R",   "/AMB-PLATINUM/AMB-GOLD-R/"));

        await db.SaveChangesAsync();

        var weekStart = WeekStart;
        for (int i = 1; i <= 6; i++)
            await SeedNewEliteTurboMember(db, $"NEW-L{i}", weekStart.AddHours(i),
                $"/AMB-PLATINUM/AMB-GOLD-L/NEW-L{i}/");
        for (int i = 1; i <= 6; i++)
            await SeedNewEliteTurboMember(db, $"NEW-R{i}", weekStart.AddHours(6 + i),
                $"/AMB-PLATINUM/AMB-GOLD-R/NEW-R{i}/");
        await db.SaveChangesAsync();

        var handler = new CalculateBoostBonusHandler(db, Clock().Object, User().Object);
        var result = await handler.Handle(new CalculateBoostBonusCommand(weekStart), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(6); // 2 AMB-GOLD-L + 2 AMB-GOLD-R + 2 AMB-PLATINUM
        result.Value.TotalAmountCalculated.Should().Be(2400);

        var earnings = await db.CommissionEarnings.ToListAsync();
        var goldEarnings     = earnings.Where(e => e.CommissionTypeId == 1).ToList();
        var platinumEarnings = earnings.Where(e => e.CommissionTypeId == 2).ToList();

        goldEarnings.Should().HaveCount(4);
        goldEarnings.Count(e => e.BeneficiaryMemberId == "AMB-GOLD-L").Should().Be(2);
        goldEarnings.Count(e => e.BeneficiaryMemberId == "AMB-GOLD-R").Should().Be(2);
        goldEarnings.Sum(e => e.Amount).Should().Be(1200); // $600 + $600

        platinumEarnings.Should().HaveCount(2);
        platinumEarnings.Should().AllSatisfy(e => e.BeneficiaryMemberId.Should().Be("AMB-PLATINUM"));
        platinumEarnings.Sum(e => e.Amount).Should().Be(1200);
    }

    [Fact]
    public async Task Handle_WhenGoldAmbHasInsufficientPlatinumCredits_GoldPaysButPlatinumSkipped()
    {
        // Chain: AMB-PLATINUM → AMB-GOLD → 6 new members.
        // AMB-GOLD: 6 Gold credits → qualifies for Gold ($600).
        // AMB-PLATINUM: 6 Platinum credits, threshold=12 → does NOT qualify.
        await using var db = InMemoryDbHelper.Create();
        db.CommissionTypes.AddRange(
            BoostType(id: 1, newMembers:  6, amount:   600, currentRankSortOrder: 3),
            BoostType(id: 2, newMembers: 12, amount: 1200, currentRankSortOrder: 5));

        db.RankDefinitions.AddRange(
            Rank(1, "Gold",     3),
            Rank(2, "Platinum", 5));
        db.MemberProfiles.AddRange(Ambassador("AMB-PLATINUM"), Ambassador("AMB-GOLD"));
        db.MemberRankHistories.AddRange(
            RankHistory("AMB-PLATINUM", 2),
            RankHistory("AMB-GOLD",     1));
        db.GenealogyTree.AddRange(
            GenealogyNode("AMB-PLATINUM", "/AMB-PLATINUM/"),
            GenealogyNode("AMB-GOLD",     "/AMB-PLATINUM/AMB-GOLD/"));

        await db.SaveChangesAsync();

        var weekStart = WeekStart;
        for (int i = 1; i <= 6; i++)
            await SeedNewEliteTurboMember(db, $"NEW-{i}", weekStart.AddHours(i),
                $"/AMB-PLATINUM/AMB-GOLD/NEW-{i}/");
        await db.SaveChangesAsync();

        var handler = new CalculateBoostBonusHandler(db, Clock().Object, User().Object);
        var result = await handler.Handle(new CalculateBoostBonusCommand(weekStart), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(2); // only Gold pays
        result.Value.TotalAmountCalculated.Should().Be(600);

        var earnings = await db.CommissionEarnings.ToListAsync();
        earnings.Should().AllSatisfy(e => e.BeneficiaryMemberId.Should().Be("AMB-GOLD"));
        earnings.Should().NotContain(e => e.BeneficiaryMemberId == "AMB-PLATINUM");
    }

    // ── 50% split payout ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CreatesTwo50PctEarningsPerQualification()
    {
        await using var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(BoostType(id: 1, newMembers: 6, amount: 600));

        db.RankDefinitions.Add(Rank(1, "Gold", 3));
        db.MemberProfiles.Add(Ambassador("AMB-GOLD"));
        db.MemberRankHistories.Add(RankHistory("AMB-GOLD", 1));
        db.GenealogyTree.Add(GenealogyNode("AMB-GOLD", "/AMB-GOLD/"));

        await db.SaveChangesAsync();

        var weekStart = WeekStart;
        for (int i = 1; i <= 6; i++)
            await SeedNewEliteTurboMember(db, $"NEW-{i}", weekStart.AddHours(i),
                $"/AMB-GOLD/NEW-{i}/");
        await db.SaveChangesAsync();

        var handler = new CalculateBoostBonusHandler(db, Clock().Object, User().Object);
        await handler.Handle(new CalculateBoostBonusCommand(weekStart), CancellationToken.None);

        var earnings = await db.CommissionEarnings
            .Where(e => e.BeneficiaryMemberId == "AMB-GOLD")
            .ToListAsync();

        earnings.Should().HaveCount(2);
        earnings.Sum(e => e.Amount).Should().Be(600);
        earnings.Should().Contain(e => e.Notes == "boost-qualification-50pct" && e.Amount == 300);
        earnings.Should().Contain(e => e.Notes == "boost-rebill-50pct"        && e.Amount == 300);
    }

    // ── Payment date rules ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenEarnedDay1to15_QualificationPayDateIs30thSameMonth()
    {
        // Earned Jan 10 → qualification payout on Jan 30
        var earnedDate = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        // weekStart = Monday of that week
        var weekStart = earnedDate.Date.AddDays(-(((int)earnedDate.DayOfWeek + 6) % 7));

        await using var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(BoostType(id: 1, newMembers: 6, amount: 600));

        db.RankDefinitions.Add(Rank(1, "Gold", 3));
        db.MemberProfiles.Add(Ambassador("AMB-GOLD"));
        db.MemberRankHistories.Add(RankHistory("AMB-GOLD", 1));
        db.GenealogyTree.Add(GenealogyNode("AMB-GOLD", "/AMB-GOLD/"));

        await db.SaveChangesAsync();

        for (int i = 1; i <= 6; i++)
            await SeedNewEliteTurboMember(db, $"NEW-{i}", weekStart.AddHours(i),
                $"/AMB-GOLD/NEW-{i}/");
        await db.SaveChangesAsync();

        var handler = new CalculateBoostBonusHandler(db, Clock(earnedDate).Object, User().Object);
        await handler.Handle(new CalculateBoostBonusCommand(weekStart), CancellationToken.None);

        var qualEarning = await db.CommissionEarnings
            .FirstAsync(e => e.Notes == "boost-qualification-50pct");

        qualEarning.PaymentDate.Should().Be(new DateTime(2026, 1, 30));
    }

    [Fact]
    public async Task Handle_WhenEarnedDay16to31_QualificationPayDateIs15thNextMonth()
    {
        // Earned Jan 20 → qualification payout on Feb 15
        var earnedDate = new DateTime(2026, 1, 20, 12, 0, 0, DateTimeKind.Utc);
        var weekStart  = earnedDate.Date.AddDays(-(((int)earnedDate.DayOfWeek + 6) % 7));

        await using var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(BoostType(id: 1, newMembers: 6, amount: 600));

        db.RankDefinitions.Add(Rank(1, "Gold", 3));
        db.MemberProfiles.Add(Ambassador("AMB-GOLD"));
        db.MemberRankHistories.Add(RankHistory("AMB-GOLD", 1));
        db.GenealogyTree.Add(GenealogyNode("AMB-GOLD", "/AMB-GOLD/"));

        await db.SaveChangesAsync();

        for (int i = 1; i <= 6; i++)
            await SeedNewEliteTurboMember(db, $"NEW-{i}", weekStart.AddHours(i),
                $"/AMB-GOLD/NEW-{i}/");
        await db.SaveChangesAsync();

        var handler = new CalculateBoostBonusHandler(db, Clock(earnedDate).Object, User().Object);
        await handler.Handle(new CalculateBoostBonusCommand(weekStart), CancellationToken.None);

        var qualEarning = await db.CommissionEarnings
            .FirstAsync(e => e.Notes == "boost-qualification-50pct");

        qualEarning.PaymentDate.Should().Be(new DateTime(2026, 2, 15));
    }

    [Fact]
    public async Task Handle_RebillPayDateIsOneMonthAfterQualificationPayDate()
    {
        // Earned Jan 10 → qualification Jan 30, rebill Feb 28 (AddMonths(1))
        var earnedDate = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        var weekStart  = earnedDate.Date.AddDays(-(((int)earnedDate.DayOfWeek + 6) % 7));

        await using var db = InMemoryDbHelper.Create();
        db.CommissionTypes.Add(BoostType(id: 1, newMembers: 6, amount: 600));

        db.RankDefinitions.Add(Rank(1, "Gold", 3));
        db.MemberProfiles.Add(Ambassador("AMB-GOLD"));
        db.MemberRankHistories.Add(RankHistory("AMB-GOLD", 1));
        db.GenealogyTree.Add(GenealogyNode("AMB-GOLD", "/AMB-GOLD/"));

        await db.SaveChangesAsync();

        for (int i = 1; i <= 6; i++)
            await SeedNewEliteTurboMember(db, $"NEW-{i}", weekStart.AddHours(i),
                $"/AMB-GOLD/NEW-{i}/");
        await db.SaveChangesAsync();

        var handler = new CalculateBoostBonusHandler(db, Clock(earnedDate).Object, User().Object);
        await handler.Handle(new CalculateBoostBonusCommand(weekStart), CancellationToken.None);

        var qualEarning   = await db.CommissionEarnings.FirstAsync(e => e.Notes == "boost-qualification-50pct");
        var rebillEarning = await db.CommissionEarnings.FirstAsync(e => e.Notes == "boost-rebill-50pct");

        rebillEarning.PaymentDate.Should().Be(qualEarning.PaymentDate.AddMonths(1));
    }
}
