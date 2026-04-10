using MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetCeoDashboard;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Dashboards;

public class GetCeoDashboardHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> Clock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static MemberProfile BuildMember(
        string memberId,
        MemberAccountStatus status = MemberAccountStatus.Active,
        MemberType type = MemberType.Ambassador,
        DateTime? enrollDate = null,
        string country = "US") => new()
    {
        MemberId = memberId,
        FirstName = "Test",
        LastName = "User",
        Email = $"{memberId}@test.com",
        Country = country,
        Status = status,
        MemberType = type,
        IsDeleted = false,
        EnrollDate = enrollDate ?? FixedNow.AddDays(-30),
        CreationDate = FixedNow.AddDays(-30),
        LastUpdateDate = FixedNow,
        CreatedBy = "seed"
    };

    private static CommissionEarning BuildCommission(
        CommissionEarningStatus status, decimal amount, DateTime? earnedDate = null) => new()
    {
        BeneficiaryMemberId = "AMB-001",
        CommissionTypeId = 1,
        Amount = amount,
        Status = status,
        EarnedDate = earnedDate ?? FixedNow.AddDays(-5),
        CreationDate = FixedNow.AddDays(-5),
        LastUpdateDate = FixedNow,
        CreatedBy = "seed"
    };

    private static Orders BuildOrder(decimal amount, DateTime orderDate,
        OrderStatus status = OrderStatus.Completed) => new()
    {
        MemberId = "AMB-001",
        TotalAmount = amount,
        OrderDate = orderDate,
        Status = status,
        CreationDate = orderDate,
        LastUpdateDate = orderDate,
        CreatedBy = "seed"
    };

    [Fact]
    public async Task Handle_WhenNoData_ReturnsZeroValues()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new GetCeoDashboardHandler(db, Clock().Object);

        var result = await handler.Handle(new GetCeoDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalMembers.Should().Be(0);
        result.Value.ActiveMembers.Should().Be(0);
        result.Value.RevenueThisMonth.Should().Be(0m);
        result.Value.CommissionsPaidAllTime.Should().Be(0m);
        result.Value.CommissionsPending.Should().Be(0m);
        result.Value.ActiveSubscriptions.Should().Be(0);
        result.Value.TicketsOpen.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CountsMembersByStatus()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-001", MemberAccountStatus.Active),
            BuildMember("AMB-002", MemberAccountStatus.Active),
            BuildMember("AMB-003", MemberAccountStatus.Inactive),
            BuildMember("AMB-004", MemberAccountStatus.Suspended),
            BuildMember("AMB-005", MemberAccountStatus.Terminated)
        );
        await db.SaveChangesAsync();

        var handler = new GetCeoDashboardHandler(db, Clock().Object);
        var result = await handler.Handle(new GetCeoDashboardQuery(), CancellationToken.None);

        result.Value!.TotalMembers.Should().Be(5);
        result.Value.ActiveMembers.Should().Be(2);
        result.Value.InactiveMembers.Should().Be(1);
        result.Value.SuspendedMembers.Should().Be(1);
        result.Value.TerminatedMembers.Should().Be(1);
    }

    [Fact]
    public async Task Handle_CountsMembersByType()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-001", type: MemberType.Ambassador),
            BuildMember("AMB-002", type: MemberType.Ambassador),
            BuildMember("EXT-001", type: MemberType.ExternalMember)
        );
        await db.SaveChangesAsync();

        var handler = new GetCeoDashboardHandler(db, Clock().Object);
        var result = await handler.Handle(new GetCeoDashboardQuery(), CancellationToken.None);

        result.Value!.TotalAmbassadors.Should().Be(2);
        result.Value.TotalExternalMembers.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ExcludesDeletedMembersFromCounts()
    {
        await using var db = InMemoryDbHelper.Create();
        var deleted = BuildMember("AMB-DEL");
        deleted.IsDeleted = true;
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-001"),
            deleted
        );
        await db.SaveChangesAsync();

        var handler = new GetCeoDashboardHandler(db, Clock().Object);
        var result = await handler.Handle(new GetCeoDashboardQuery(), CancellationToken.None);

        result.Value!.TotalMembers.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SumsRevenueForCurrentMonth_ExcludesCancelledOrders()
    {
        await using var db = InMemoryDbHelper.Create();
        var startOfMonth = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.Orders.AddRangeAsync(
            BuildOrder(500m, startOfMonth, OrderStatus.Completed),   // this month
            BuildOrder(300m, FixedNow.AddDays(-2), OrderStatus.Completed), // this month
            BuildOrder(100m, FixedNow.AddDays(-1), OrderStatus.Cancelled), // excluded
            BuildOrder(1000m, new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc), OrderStatus.Completed) // last month
        );
        await db.SaveChangesAsync();

        var handler = new GetCeoDashboardHandler(db, Clock().Object);
        var result = await handler.Handle(new GetCeoDashboardQuery(), CancellationToken.None);

        result.Value!.RevenueThisMonth.Should().Be(800m);
        result.Value.OrdersThisMonth.Should().Be(2);
    }

    [Fact]
    public async Task Handle_SumsCommissionsCorrectly()
    {
        await using var db = InMemoryDbHelper.Create();
        var startOfMonth = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.CommissionEarnings.AddRangeAsync(
            BuildCommission(CommissionEarningStatus.Paid, 100m, FixedNow.AddMonths(-2)),  // paid, old
            BuildCommission(CommissionEarningStatus.Paid, 200m, startOfMonth),             // paid, this month
            BuildCommission(CommissionEarningStatus.Pending, 50m)                          // pending
        );
        await db.SaveChangesAsync();

        var handler = new GetCeoDashboardHandler(db, Clock().Object);
        var result = await handler.Handle(new GetCeoDashboardQuery(), CancellationToken.None);

        result.Value!.CommissionsPaidAllTime.Should().Be(300m);
        result.Value.CommissionsPaidThisMonth.Should().Be(200m);
        result.Value.CommissionsPending.Should().Be(50m);
    }

    [Fact]
    public async Task Handle_CountsActiveSubscriptions()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipSubscriptions.AddRangeAsync(
            new MembershipSubscription { MemberId = "AMB-001", MembershipLevelId = 1, SubscriptionStatus = MembershipStatus.Active, ChangeReason = SubscriptionChangeReason.New, CreationDate = FixedNow, CreatedBy = "seed", LastUpdateDate = FixedNow },
            new MembershipSubscription { MemberId = "AMB-002", MembershipLevelId = 1, SubscriptionStatus = MembershipStatus.Active, ChangeReason = SubscriptionChangeReason.New, CreationDate = FixedNow, CreatedBy = "seed", LastUpdateDate = FixedNow },
            new MembershipSubscription { MemberId = "AMB-003", MembershipLevelId = 1, SubscriptionStatus = MembershipStatus.Cancelled, ChangeReason = SubscriptionChangeReason.Cancellation, CreationDate = FixedNow, CreatedBy = "seed", LastUpdateDate = FixedNow }
        );
        await db.SaveChangesAsync();

        var handler = new GetCeoDashboardHandler(db, Clock().Object);
        var result = await handler.Handle(new GetCeoDashboardQuery(), CancellationToken.None);

        result.Value!.ActiveSubscriptions.Should().Be(2);
    }

    [Fact]
    public async Task Handle_CountsTicketsByStatus()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.TicketCategories.AddAsync(new TicketCategory
        {
            Id = 1, Name = "General", CreationDate = FixedNow, CreatedBy = "seed"
        });
        await db.Tickets.AddRangeAsync(
            new Ticket { MemberId = "AMB-001", Subject = "T1", Body = "B1", Status = TicketStatus.Open, Priority = TicketPriority.Normal, Channel = TicketChannel.Portal, CategoryId = 1, TicketNumber = "HD-001", CreationDate = FixedNow, CreatedBy = "seed", LastUpdateDate = FixedNow },
            new Ticket { MemberId = "AMB-002", Subject = "T2", Body = "B2", Status = TicketStatus.Open, Priority = TicketPriority.Normal, Channel = TicketChannel.Portal, CategoryId = 1, TicketNumber = "HD-002", CreationDate = FixedNow, CreatedBy = "seed", LastUpdateDate = FixedNow },
            new Ticket { MemberId = "AMB-003", Subject = "T3", Body = "B3", Status = TicketStatus.InProgress, Priority = TicketPriority.Critical, Channel = TicketChannel.Portal, CategoryId = 1, TicketNumber = "HD-003", CreationDate = FixedNow, CreatedBy = "seed", LastUpdateDate = FixedNow }
        );
        await db.SaveChangesAsync();

        var handler = new GetCeoDashboardHandler(db, Clock().Object);
        var result = await handler.Handle(new GetCeoDashboardQuery(), CancellationToken.None);

        result.Value!.TicketsOpen.Should().Be(2);
        result.Value.TicketsInProgress.Should().Be(1);
        result.Value.TicketsCritical.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ReturnsRecentMembersLimitedToTen()
    {
        await using var db = InMemoryDbHelper.Create();
        for (int i = 0; i < 15; i++)
        {
            await db.MemberProfiles.AddAsync(
                BuildMember($"AMB-{i:D3}", enrollDate: FixedNow.AddDays(-i)));
        }
        await db.SaveChangesAsync();

        var handler = new GetCeoDashboardHandler(db, Clock().Object);
        var result = await handler.Handle(new GetCeoDashboardQuery(), CancellationToken.None);

        result.Value!.RecentMembers.Should().HaveCount(10);
    }

    [Fact]
    public async Task Handle_CountsPaymentsPendingAndFailed()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.PaymentHistories.AddRangeAsync(
            new PaymentHistory { OrderId = "O1", MemberId = "AMB-001", Amount = 50m, GatewayName = "Stripe", TransactionStatus = PaymentHistoryTransactionStatus.Pending, CreationDate = FixedNow, CreatedBy = "seed", LastUpdateDate = FixedNow },
            new PaymentHistory { OrderId = "O2", MemberId = "AMB-001", Amount = 50m, GatewayName = "Stripe", TransactionStatus = PaymentHistoryTransactionStatus.Failed, CreationDate = FixedNow, CreatedBy = "seed", LastUpdateDate = FixedNow },
            new PaymentHistory { OrderId = "O3", MemberId = "AMB-001", Amount = 50m, GatewayName = "Stripe", TransactionStatus = PaymentHistoryTransactionStatus.Captured, CreationDate = FixedNow, CreatedBy = "seed", LastUpdateDate = FixedNow }
        );
        await db.SaveChangesAsync();

        var handler = new GetCeoDashboardHandler(db, Clock().Object);
        var result = await handler.Handle(new GetCeoDashboardQuery(), CancellationToken.None);

        result.Value!.PaymentsPending.Should().Be(1);
        result.Value.PaymentsFailed.Should().Be(1);
    }
}
