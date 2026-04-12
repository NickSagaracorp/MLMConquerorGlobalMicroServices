using MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateFastStartBonus;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.CommissionEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.CommissionEngine.Tests.Features;

public class CalculateFastStartBonusHandlerTests
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

    private static Orders BuildCompletedOrder(string id, string memberId, decimal total = 80) => new()
    {
        Id             = id,
        MemberId       = memberId,
        TotalAmount    = total,
        Status         = OrderStatus.Completed,
        OrderDate      = FixedNow,
        CreatedBy      = "seed",
        CreationDate   = FixedNow,
        LastUpdateDate = FixedNow
    };

    private static MemberProfile BuildMember(string memberId, string? sponsor = null,
        Guid? userId = null) => new()
    {
        MemberId        = memberId,
        UserId          = userId ?? Guid.Empty,
        FirstName       = "Test",
        LastName        = "User",
        MemberType      = MemberType.Ambassador,
        EnrollDate      = FixedNow.AddMonths(-1),
        Country         = "US",
        SponsorMemberId = sponsor,
        CreatedBy       = "seed",
        LastUpdateDate  = FixedNow
    };

    private static CommissionType BuildFsbType(int id, int levelNo, int triggerOrder = 1,
        decimal? fixedAmount = 150) => new()
    {
        Id               = id,
        Name             = $"FSB-L{levelNo}-W{triggerOrder}",
        IsActive         = true,
        IsPaidOnSignup   = true,
        IsSponsorBonus   = false,
        LevelNo          = levelNo,
        TriggerOrder     = triggerOrder,
        FixedAmount      = fixedAmount,
        Percentage       = 0,
        PaymentDelayDays = 0,
        CreatedBy        = "seed",
        CreationDate     = FixedNow
    };

    private static ProductCommission BuildProductCommission(string productId,
        bool triggerFsb = true) => new()
    {
        ProductId             = productId,
        TriggerFastStartBonus = triggerFsb,
        CreatedBy             = "seed",
        CreationDate          = FixedNow
    };

    [Fact]
    public async Task Handle_WhenOrderNotFound_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new CalculateFastStartBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateFastStartBonusCommand("ORD-GHOST", "AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenNoFsbProducts_SkipsWithZeroEarnings()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Orders.AddAsync(BuildCompletedOrder("ORD-001", "AMB-001"));
        // OrderDetails reference product "P-1" which has no ProductCommission with TriggerFastStartBonus
        await db.OrderDetails.AddAsync(new OrderDetail
        {
            OrderId = "ORD-001", ProductId = "P-NO-FSB",
            Quantity = 1, UnitPrice = 80, CreatedBy = "seed", CreationDate = FixedNow
        });
        await db.SaveChangesAsync();

        var handler = new CalculateFastStartBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateFastStartBonusCommand("ORD-001", "AMB-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenNoFsbCommissionTypes_SkipsWithZeroEarnings()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Orders.AddAsync(BuildCompletedOrder("ORD-002", "AMB-002"));
        await db.OrderDetails.AddAsync(new OrderDetail
        {
            OrderId = "ORD-002", ProductId = "1",
            Quantity = 1, UnitPrice = 80, CreatedBy = "seed", CreationDate = FixedNow
        });
        await db.ProductCommissions.AddAsync(BuildProductCommission("1", triggerFsb: true));
        // No CommissionTypes seeded
        await db.SaveChangesAsync();

        var handler = new CalculateFastStartBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new CalculateFastStartBonusCommand("ORD-002", "AMB-002"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenCalledTwice_IsIdempotent()
    {
        await using var db = InMemoryDbHelper.Create();
        var sponsorUserId = Guid.NewGuid();
        var sponsor = BuildMember("AMB-SPONSOR", userId: sponsorUserId);
        var buyer   = BuildMember("AMB-BUYER",   sponsor: "AMB-SPONSOR");

        await db.MemberProfiles.AddRangeAsync(sponsor, buyer);
        await db.Orders.AddAsync(BuildCompletedOrder("ORD-003", "AMB-BUYER"));
        await db.OrderDetails.AddAsync(new OrderDetail
        {
            OrderId = "ORD-003", ProductId = "1",
            Quantity = 1, UnitPrice = 80, CreatedBy = "seed", CreationDate = FixedNow
        });
        await db.ProductCommissions.AddAsync(BuildProductCommission("1", triggerFsb: true));
        await db.CommissionTypes.AddAsync(BuildFsbType(id: 1, levelNo: 1, triggerOrder: 1, fixedAmount: 150));

        // Sponsor is in window 1 — MemberId matches sponsor.UserId so the handler can find it
        var countdown = new MemberCommissionCountDown
        {
            MemberId             = sponsorUserId,
            Member               = sponsor,
            FastStartBonus1Start = FixedNow.AddDays(-3),
            FastStartBonus1End   = FixedNow.AddDays(11),
            FastStartBonus2Start = FixedNow.AddDays(12),
            FastStartBonus2End   = FixedNow.AddDays(19),
            FastStartBonus3Start = FixedNow.AddDays(20),
            FastStartBonus3End   = FixedNow.AddDays(27),
            CreatedBy            = "seed",
            CreationDate         = FixedNow,
            LastUpdateDate       = FixedNow
        };
        await db.CommissionCountDowns.AddAsync(countdown);
        await db.SaveChangesAsync();

        var handler = new CalculateFastStartBonusHandler(db, BuildClock().Object, BuildUser().Object);

        await handler.Handle(new CalculateFastStartBonusCommand("ORD-003", "AMB-BUYER"), CancellationToken.None);
        var result = await handler.Handle(
            new CalculateFastStartBonusCommand("ORD-003", "AMB-BUYER"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Second call should add 0 more (idempotent)
        db.CommissionEarnings.Should().HaveCount(1);
    }
}
