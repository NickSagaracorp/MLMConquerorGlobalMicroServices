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
        var handler = new GetFinancialDashboardHandler(db, DateTimeProvider().Object);

        var result = await handler.Handle(new GetFinancialDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalMembersActive.Should().Be(0);
        result.Value.TotalCommissionsPaid.Should().Be(0);
        result.Value.TotalCommissionsPending.Should().Be(0);
        result.Value.TotalRevenueThisMonth.Should().Be(0);
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

        var handler = new GetFinancialDashboardHandler(db, DateTimeProvider().Object);
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

        var handler = new GetFinancialDashboardHandler(db, DateTimeProvider().Object);
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

        var handler = new GetFinancialDashboardHandler(db, DateTimeProvider().Object);
        var result = await handler.Handle(new GetFinancialDashboardQuery(), CancellationToken.None);

        result.Value!.TotalRevenueThisMonth.Should().Be(800m);
    }
}
