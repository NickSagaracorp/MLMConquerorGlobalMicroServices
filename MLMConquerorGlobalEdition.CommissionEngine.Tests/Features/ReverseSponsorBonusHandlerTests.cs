using MLMConquerorGlobalEdition.CommissionEngine.Features.ReverseSponsorBonus;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.CommissionEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.CommissionEngine.Tests.Features;

public class ReverseSponsorBonusHandlerTests
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

    private static Orders BuildOrder(string id, string memberId, DateTime? orderDate = null) => new()
    {
        Id             = id,
        MemberId       = memberId,
        TotalAmount    = 80,
        Status         = OrderStatus.Completed,
        OrderDate      = orderDate ?? FixedNow.AddDays(-2),
        CreatedBy      = "seed",
        CreationDate   = FixedNow.AddDays(-2),
        LastUpdateDate = FixedNow.AddDays(-2)
    };

    private static CommissionType BuildSponsorType(int id = 10, int reverseId = 0) => new()
    {
        Id              = id,
        Name            = "SponsorBonus",
        IsActive        = true,
        IsSponsorBonus  = true,
        LevelNo         = 2,
        Amount     = 20,
        Percentage      = 0,
        ReverseId       = reverseId,
        PaymentDelayDays = 0,
        CreatedBy       = "seed",
        CreationDate    = FixedNow
    };

    private static CommissionEarning BuildEarning(string orderId, string sourceMemberId,
        int commTypeId = 10, CommissionEarningStatus status = CommissionEarningStatus.Pending) => new()
    {
        BeneficiaryMemberId = "AMB-SPONSOR",
        SourceMemberId      = sourceMemberId,
        SourceOrderId       = orderId,
        CommissionTypeId    = commTypeId,
        Amount              = 20,
        Status              = status,
        EarnedDate          = FixedNow.AddDays(-2),
        PaymentDate         = FixedNow.AddDays(5),
        PeriodDate          = FixedNow.AddDays(-2).Date,
        CreatedBy           = "seed",
        CreationDate        = FixedNow.AddDays(-2),
        LastUpdateDate      = FixedNow.AddDays(-2)
    };

    [Fact]
    public async Task Handle_WhenOrderNotFound_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new ReverseSponsorBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new ReverseSponsorBonusCommand("AMB-001", "ORD-GHOST", null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenOutsideReversalWindow_SkipsWithSuccess()
    {
        await using var db = InMemoryDbHelper.Create();
        // Order placed 20 days ago — beyond the 14-day window
        await db.Orders.AddAsync(BuildOrder("ORD-OLD", "AMB-001",
            orderDate: FixedNow.AddDays(-20)));
        await db.SaveChangesAsync();

        var handler = new ReverseSponsorBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new ReverseSponsorBonusCommand("AMB-001", "ORD-OLD", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenPendingEarning_CancelsEarningInPlace()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Orders.AddAsync(BuildOrder("ORD-001", "AMB-001"));
        await db.CommissionTypes.AddAsync(BuildSponsorType(id: 10));
        var earning = BuildEarning("ORD-001", "AMB-001", commTypeId: 10,
            status: CommissionEarningStatus.Pending);
        await db.CommissionEarnings.AddAsync(earning);
        await db.SaveChangesAsync();

        var handler = new ReverseSponsorBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new ReverseSponsorBonusCommand("AMB-001", "ORD-001", "Cancelled by member"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = db.CommissionEarnings.Single(e => e.SourceOrderId == "ORD-001");
        updated.Status.Should().Be(CommissionEarningStatus.Cancelled);
        updated.Notes.Should().Be("Cancelled by member");
    }

    [Fact]
    public async Task Handle_WhenPaidEarningWithNoReverseId_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Orders.AddAsync(BuildOrder("ORD-002", "AMB-002"));
        await db.CommissionTypes.AddAsync(BuildSponsorType(id: 10, reverseId: 0));
        var earning = BuildEarning("ORD-002", "AMB-002", commTypeId: 10,
            status: CommissionEarningStatus.Paid);
        await db.CommissionEarnings.AddAsync(earning);
        await db.SaveChangesAsync();

        var handler = new ReverseSponsorBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new ReverseSponsorBonusCommand("AMB-002", "ORD-002", null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_REVERSE_TYPE");
    }

    [Fact]
    public async Task Handle_WhenPaidEarningWithReverseType_CreatesNegativeReversal()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Orders.AddAsync(BuildOrder("ORD-003", "AMB-003"));
        await db.CommissionTypes.AddRangeAsync(
            BuildSponsorType(id: 10, reverseId: 11),
            new CommissionType
            {
                Id = 11, Name = "SponsorBonusReversal", IsActive = true, IsSponsorBonus = false,
                Percentage = 0, PaymentDelayDays = 0, CreatedBy = "seed", CreationDate = FixedNow
            });
        var earning = BuildEarning("ORD-003", "AMB-003", commTypeId: 10,
            status: CommissionEarningStatus.Paid);
        await db.CommissionEarnings.AddAsync(earning);
        await db.SaveChangesAsync();

        var handler = new ReverseSponsorBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new ReverseSponsorBonusCommand("AMB-003", "ORD-003", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(1);
        result.Value.TotalAmountCalculated.Should().Be(-20);

        var reversal = db.CommissionEarnings.Single(e => e.CommissionTypeId == 11);
        reversal.Amount.Should().Be(-20);
    }

    [Fact]
    public async Task Handle_WhenNoEarningFound_SkipsWithSuccess()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Orders.AddAsync(BuildOrder("ORD-004", "AMB-004"));
        await db.CommissionTypes.AddAsync(BuildSponsorType(id: 10));
        // No earnings for this order
        await db.SaveChangesAsync();

        var handler = new ReverseSponsorBonusHandler(db, BuildClock().Object, BuildUser().Object);

        var result = await handler.Handle(
            new ReverseSponsorBonusCommand("AMB-004", "ORD-004", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecordsCreated.Should().Be(0);
    }
}

