using MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetFinancialDashboard;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Dashboards;

public class GetFinancialDashboardHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> DateTimeProvider()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static MemberProfile BuildMember(string memberId, MemberAccountStatus status) => new()
    {
        MemberId = memberId,
        FirstName = "Test",
        LastName = "User",
        Country = "US",
        Status = status,
        MemberType = MemberType.Ambassador,
        EnrollDate = FixedNow.AddDays(-30),
        CreationDate = FixedNow.AddDays(-30),
        LastUpdateDate = FixedNow,
        CreatedBy = "seed"
    };

    private static CommissionEarning BuildCommission(CommissionEarningStatus status, decimal amount) => new()
    {
        BeneficiaryMemberId = "AMB-001",
        CommissionTypeId = 1,
        Amount = amount,
        Status = status,
        EarnedDate = FixedNow.AddDays(-5),
        PaymentDate = FixedNow.AddDays(2),
        CreationDate = FixedNow.AddDays(-5),
        LastUpdateDate = FixedNow.AddDays(-5),
        CreatedBy = "seed"
    };

    private static Orders BuildOrder(decimal amount, DateTime orderDate) => new()
    {
        MemberId = "AMB-001",
        TotalAmount = amount,
        OrderDate = orderDate,
        Status = OrderStatus.Completed,
        CreationDate = orderDate,
        LastUpdateDate = orderDate,
        CreatedBy = "seed"
    };

    [Fact]
    public async Task Handle_WhenNoData_ReturnsZeroValues()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new GetFinancialDashboardHandler(db, DateTimeProvider().Object, new NoOpCacheService());

        var result = await handler.Handle(new GetFinancialDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalMembersActive.Should().Be(0);
        result.Value.TotalCommissionsPaid.Should().Be(0);
        result.Value.TotalCommissionsPending.Should().Be(0);
        result.Value.TotalRevenue.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CountsOnlyActiveMembers()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-001", MemberAccountStatus.Active),
            BuildMember("AMB-002", MemberAccountStatus.Active),
            BuildMember("AMB-003", MemberAccountStatus.Inactive));
        await db.SaveChangesAsync();

        var handler = new GetFinancialDashboardHandler(db, DateTimeProvider().Object, new NoOpCacheService());
        var result = await handler.Handle(new GetFinancialDashboardQuery(), CancellationToken.None);

        result.Value!.TotalMembersActive.Should().Be(2);
    }

    [Fact]
    public async Task Handle_SumsPaidCommissions()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.CommissionEarnings.AddRangeAsync(
            BuildCommission(CommissionEarningStatus.Paid, 100m),
            BuildCommission(CommissionEarningStatus.Paid, 200m),
            BuildCommission(CommissionEarningStatus.Pending, 50m));
        await db.SaveChangesAsync();

        var handler = new GetFinancialDashboardHandler(db, DateTimeProvider().Object, new NoOpCacheService());
        var result = await handler.Handle(new GetFinancialDashboardQuery(), CancellationToken.None);

        result.Value!.TotalCommissionsPaid.Should().Be(300m);
        result.Value.TotalCommissionsPending.Should().Be(50m);
    }

    [Fact]
    public async Task Handle_SumsRevenueOnlyForCurrentMonth()
    {
        await using var db = InMemoryDbHelper.Create();
        var startOfMonth = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.Orders.AddRangeAsync(
            BuildOrder(500m, startOfMonth),                    // this month
            BuildOrder(300m, FixedNow.AddDays(-5)),            // this month
            BuildOrder(1000m, new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc))); // last month
        await db.SaveChangesAsync();

        var handler = new GetFinancialDashboardHandler(db, DateTimeProvider().Object, new NoOpCacheService());
        var result = await handler.Handle(new GetFinancialDashboardQuery(), CancellationToken.None);

        result.Value!.TotalRevenue.Should().Be(800m);
    }

    [Fact]
    public async Task Handle_WithExplicitRange_FiltersCommissionsAndRevenueToWindow()
    {
        await using var db = InMemoryDbHelper.Create();
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to   = new DateTime(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        // In-window
        db.CommissionEarnings.Add(new CommissionEarning { BeneficiaryMemberId="AMB-001", CommissionTypeId=1, Amount=100m, Status=CommissionEarningStatus.Paid, EarnedDate=new DateTime(2026,1,15,0,0,0,DateTimeKind.Utc), PaymentDate=new DateTime(2026,1,17,0,0,0,DateTimeKind.Utc), CreationDate=from, LastUpdateDate=from, CreatedBy="seed" });
        db.Orders.Add(BuildOrder(500m, new DateTime(2026,1,10,0,0,0,DateTimeKind.Utc)));
        // Out-of-window (February)
        db.CommissionEarnings.Add(new CommissionEarning { BeneficiaryMemberId="AMB-001", CommissionTypeId=1, Amount=999m, Status=CommissionEarningStatus.Paid, EarnedDate=new DateTime(2026,2,15,0,0,0,DateTimeKind.Utc), PaymentDate=new DateTime(2026,2,17,0,0,0,DateTimeKind.Utc), CreationDate=from, LastUpdateDate=from, CreatedBy="seed" });
        db.Orders.Add(BuildOrder(7777m, new DateTime(2026,2,10,0,0,0,DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var handler = new GetFinancialDashboardHandler(db, DateTimeProvider().Object, new NoOpCacheService());
        var result  = await handler.Handle(new GetFinancialDashboardQuery(from, to), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RangeFrom.Should().Be(from);
        result.Value.RangeTo.Should().Be(to);
        result.Value.TotalCommissionsPaid.Should().Be(100m);   // Feb 999 excluded
        result.Value.TotalRevenue.Should().Be(500m);            // Feb 7777 excluded
        result.Value.NetCashFlow.Should().Be(400m);             // 500 - 100
        result.Value.CommissionToRevenuePct.Should().Be(20m);  // 100/500
    }
}
