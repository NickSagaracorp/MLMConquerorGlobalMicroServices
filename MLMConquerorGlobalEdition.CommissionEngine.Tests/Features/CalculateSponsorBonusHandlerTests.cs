using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateSponsorBonus;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.CommissionEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.CommissionEngine.Tests.Features;

public class CalculateSponsorBonusHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> BuildClock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static Mock<ICurrentUserService> BuildUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("system");
        return m;
    }

    private static Orders BuildOrder(string id, string memberId,
        OrderStatus status = OrderStatus.Completed, decimal total = 100) => new()
    {
        Id             = id,
        MemberId       = memberId,
        TotalAmount    = total,
        Status         = status,
        OrderDate      = FixedNow,
        CreatedBy      = "seed",
        CreationDate   = FixedNow,
        LastUpdateDate = FixedNow
    };

    private static MemberProfile BuildMember(string memberId, string? sponsor = null) => new()
    {
        MemberId       = memberId,
        FirstName      = "Test",
        LastName       = "User",
        MemberType     = MemberType.Ambassador,
        EnrollDate     = FixedNow,
        Country        = "US",
        SponsorMemberId = sponsor,
        CreatedBy      = "seed",
        LastUpdateDate = FixedNow
    };

    private static CommissionType BuildSponsorBonusType(int id = 10, int levelNo = 2,
        decimal? Amount = 20) => new()
    {
        Id              = id,
        Name            = "SponsorBonus-VIP",
        IsActive        = true,
        IsSponsorBonus  = true,
        LevelNo         = levelNo,
        Amount     = Amount,
        Percentage      = 10,
        PaymentDelayDays = 0,
        CreatedBy       = "seed",
        CreationDate    = FixedNow
    };

    private static Product BuildProduct(string id, int? membershipLevelId) => new()
    {
        Id             = id,
        Name           = "VIP Pack",
        Description    = "desc",
        ImageUrl       = "https://cdn.example.com/img.png",
        MonthlyFee     = 80,
        SetupFee       = 0,
        MembershipLevelId = membershipLevelId,
        IsActive       = true,
        CreatedBy      = "seed",
        CreationDate   = FixedNow,
        LastUpdateDate = FixedNow
    };

    [Fact]
    public async Task Handle_WhenOrderNotFound_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new CalculateSponsorBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateSponsorBonusCommand("AMB-NEW", "ORD-GHOST"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenMemberHasNoSponsor_SkipsWithSuccess()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Orders.AddAsync(BuildOrder("ORD-001", "AMB-001"));
        await db.MemberProfiles.AddAsync(BuildMember("AMB-001", sponsor: null));
        await db.SaveChangesAsync();

        var handler = new CalculateSponsorBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateSponsorBonusCommand("AMB-001", "ORD-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenLifestyleAmbassadorProduct_SkipsWithSuccess()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Orders.AddAsync(BuildOrder("ORD-002", "AMB-002"));
        await db.MemberProfiles.AddAsync(BuildMember("AMB-002", sponsor: "AMB-SPONSOR"));
        await db.Products.AddAsync(BuildProduct("P-LIFESTYLE", membershipLevelId: 1));
        await db.OrderDetails.AddAsync(new OrderDetail
        {
            OrderId      = "ORD-002",
            ProductId    = "P-LIFESTYLE",
            Quantity     = 1,
            UnitPrice    = 50,
            CreatedBy    = "seed",
            CreationDate = FixedNow
        });
        await db.SaveChangesAsync();

        var handler = new CalculateSponsorBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateSponsorBonusCommand("AMB-002", "ORD-002"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenValidVipSignup_CreatesSponsorBonusEarning()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Orders.AddAsync(BuildOrder("ORD-003", "AMB-003"));
        await db.MemberProfiles.AddAsync(BuildMember("AMB-003", sponsor: "AMB-SPONSOR"));
        await db.Products.AddAsync(BuildProduct("P-VIP", membershipLevelId: 2));
        await db.CommissionTypes.AddAsync(BuildSponsorBonusType(id: 10, levelNo: 2, Amount: 20));
        await db.OrderDetails.AddAsync(new OrderDetail
        {
            OrderId      = "ORD-003",
            ProductId    = "P-VIP",
            Quantity     = 1,
            UnitPrice    = 80,
            CreatedBy    = "seed",
            CreationDate = FixedNow
        });
        await db.SaveChangesAsync();

        var handler = new CalculateSponsorBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateSponsorBonusCommand("AMB-003", "ORD-003"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(1);
        result.Value.TotalAmountCalculated.Should().Be(20);

        var earning = db.CommissionEarnings.Single();
        earning.BeneficiaryMemberId.Should().Be("AMB-SPONSOR");
        earning.Amount.Should().Be(20);
    }

    [Fact]
    public async Task Handle_WhenCalledTwice_IsIdempotent()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Orders.AddAsync(BuildOrder("ORD-004", "AMB-004"));
        await db.MemberProfiles.AddAsync(BuildMember("AMB-004", sponsor: "AMB-SPONSOR"));
        await db.Products.AddAsync(BuildProduct("P-VIP", membershipLevelId: 2));
        await db.CommissionTypes.AddAsync(BuildSponsorBonusType(id: 10, levelNo: 2, Amount: 20));
        await db.OrderDetails.AddAsync(new OrderDetail
        {
            OrderId      = "ORD-004",
            ProductId    = "P-VIP",
            Quantity     = 1,
            UnitPrice    = 80,
            CreatedBy    = "seed",
            CreationDate = FixedNow
        });
        await db.SaveChangesAsync();

        var handler = new CalculateSponsorBonusHandler(db, BuildClock().Object, BuildUser().Object);
        await handler.Handle(new CalculateSponsorBonusCommand("AMB-004", "ORD-004"), CancellationToken.None);
        var result = await handler.Handle(
            new CalculateSponsorBonusCommand("AMB-004", "ORD-004"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(0);
        db.CommissionEarnings.Should().HaveCount(1);
    }

    private static CorporatePromo BuildPromo(
        int sponsorMultiplier = 1, int builderMultiplier = 1, string id = "PROMO-1")
    {
        // AuditInterceptor stamps Order.CreationDate at real wall-clock-now on save —
        // it doesn't honor BuildOrder's FixedNow. Anchor the promo window to UtcNow
        // so the order's actual saved CreationDate falls inside it.
        var realNow = DateTime.UtcNow;
        return new CorporatePromo
        {
            Id                     = id,
            Title                  = "Test Promo",
            StartDate              = realNow.AddDays(-7),
            EndDate                = realNow.AddDays(7),
            IsActive               = true,
            SponsorBonusMultiplier = sponsorMultiplier,
            BuilderBonusMultiplier = builderMultiplier,
            CreatedBy              = "seed",
            CreationDate           = realNow.AddDays(-7),
            LastUpdateDate         = realNow.AddDays(-7)
        };
    }

    [Fact]
    public async Task Handle_WhenActivePromoHas3xSponsorMultiplier_AmountIsTripled()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Orders.AddAsync(BuildOrder("ORD-PROMO3X", "AMB-PROMO3X"));
        await db.MemberProfiles.AddAsync(BuildMember("AMB-PROMO3X", sponsor: "AMB-SPONSOR"));
        await db.Products.AddAsync(BuildProduct("P-VIP", membershipLevelId: 2));
        await db.CommissionTypes.AddAsync(BuildSponsorBonusType(id: 10, levelNo: 2, Amount: 20));
        await db.OrderDetails.AddAsync(new OrderDetail
        {
            OrderId = "ORD-PROMO3X", ProductId = "P-VIP", Quantity = 1, UnitPrice = 80,
            CreatedBy = "seed", CreationDate = FixedNow
        });
        await db.CorporatePromos.AddAsync(BuildPromo(sponsorMultiplier: 3));
        await db.SaveChangesAsync();

        var handler = new CalculateSponsorBonusHandler(db, BuildClock().Object, BuildUser().Object);
        var result  = await handler.Handle(
            new CalculateSponsorBonusCommand("AMB-PROMO3X", "ORD-PROMO3X"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var earning = db.CommissionEarnings.Single();
        earning.Amount.Should().Be(60); // 20 × 3
        earning.Notes.Should().Contain("3×").And.Contain("Test Promo");
    }

    [Fact]
    public async Task Handle_WhenActivePromoHas5xSponsorMultiplier_AmountIsQuintupled()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Orders.AddAsync(BuildOrder("ORD-PROMO5X", "AMB-PROMO5X"));
        await db.MemberProfiles.AddAsync(BuildMember("AMB-PROMO5X", sponsor: "AMB-SPONSOR"));
        await db.Products.AddAsync(BuildProduct("P-VIP", membershipLevelId: 2));
        await db.CommissionTypes.AddAsync(BuildSponsorBonusType(id: 10, levelNo: 2, Amount: 20));
        await db.OrderDetails.AddAsync(new OrderDetail
        {
            OrderId = "ORD-PROMO5X", ProductId = "P-VIP", Quantity = 1, UnitPrice = 80,
            CreatedBy = "seed", CreationDate = FixedNow
        });
        await db.CorporatePromos.AddAsync(BuildPromo(sponsorMultiplier: 5));
        await db.SaveChangesAsync();

        var handler = new CalculateSponsorBonusHandler(db, BuildClock().Object, BuildUser().Object);
        var result  = await handler.Handle(
            new CalculateSponsorBonusCommand("AMB-PROMO5X", "ORD-PROMO5X"), CancellationToken.None);

        db.CommissionEarnings.Single().Amount.Should().Be(100); // 20 × 5
    }

    [Fact]
    public async Task Handle_WhenPromoMultiplierIs1_PaysBaseAmountAndDoesNotStampPromoNote()
    {
        // Multiplier=1 means "no boost" — the promo row exists (maybe it has
        // BuilderBonusMultiplier > 1) but Sponsor side stays at base amount.
        await using var db = InMemoryDbHelper.Create();
        await db.Orders.AddAsync(BuildOrder("ORD-NOBOOST", "AMB-NOBOOST"));
        await db.MemberProfiles.AddAsync(BuildMember("AMB-NOBOOST", sponsor: "AMB-SPONSOR"));
        await db.Products.AddAsync(BuildProduct("P-VIP", membershipLevelId: 2));
        await db.CommissionTypes.AddAsync(BuildSponsorBonusType(id: 10, levelNo: 2, Amount: 20));
        await db.OrderDetails.AddAsync(new OrderDetail
        {
            OrderId = "ORD-NOBOOST", ProductId = "P-VIP", Quantity = 1, UnitPrice = 80,
            CreatedBy = "seed", CreationDate = FixedNow
        });
        await db.CorporatePromos.AddAsync(BuildPromo(sponsorMultiplier: 1, builderMultiplier: 4));
        await db.SaveChangesAsync();

        var handler = new CalculateSponsorBonusHandler(db, BuildClock().Object, BuildUser().Object);
        await handler.Handle(new CalculateSponsorBonusCommand("AMB-NOBOOST", "ORD-NOBOOST"), CancellationToken.None);

        var earning = db.CommissionEarnings.Single();
        earning.Amount.Should().Be(20); // base only
        earning.Notes.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenNoSponsorBonusTypeConfigured_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Orders.AddAsync(BuildOrder("ORD-005", "AMB-005"));
        await db.MemberProfiles.AddAsync(BuildMember("AMB-005", sponsor: "AMB-SPONSOR"));
        await db.Products.AddAsync(BuildProduct("P-ELITE", membershipLevelId: 3));
        await db.OrderDetails.AddAsync(new OrderDetail
        {
            OrderId      = "ORD-005",
            ProductId    = "P-ELITE",
            Quantity     = 1,
            UnitPrice    = 120,
            CreatedBy    = "seed",
            CreationDate = FixedNow
        });
        // No CommissionType seeded
        await db.SaveChangesAsync();

        var handler = new CalculateSponsorBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateSponsorBonusCommand("AMB-005", "ORD-005"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_SPONSOR_BONUS_TYPE");
    }
}

