using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateSponsorBonus;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.CommissionEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
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
        decimal? fixedAmount = 20) => new()
    {
        Id              = id,
        Name            = "SponsorBonus-VIP",
        IsActive        = true,
        IsSponsorBonus  = true,
        LevelNo         = levelNo,
        FixedAmount     = fixedAmount,
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
        await db.CommissionTypes.AddAsync(BuildSponsorBonusType(id: 10, levelNo: 2, fixedAmount: 20));
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
        await db.CommissionTypes.AddAsync(BuildSponsorBonusType(id: 10, levelNo: 2, fixedAmount: 20));
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
