using MLMConquerorGlobalEdition.AdminAPI.Features.Dashboards.GetHealthDashboard;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Dashboards;

public class GetHealthDashboardHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

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

    private static PaymentHistory BuildPayment(PaymentHistoryTransactionStatus status) => new()
    {
        OrderId = Guid.NewGuid().ToString(),
        MemberId = "AMB-001",
        Amount = 99m,
        GatewayName = "Stripe",
        TransactionStatus = status,
        CreationDate = FixedNow,
        LastUpdateDate = FixedNow,
        CreatedBy = "seed"
    };

    private static Ticket BuildTicket(TicketStatus status) => new()
    {
        MemberId = "AMB-001",
        CategoryId = 1,
        Subject = "Issue",
        Body = "Description",
        Status = status,
        Priority = TicketPriority.Normal,
        CreationDate = FixedNow,
        LastUpdateDate = FixedNow,
        CreatedBy = "seed"
    };

    [Fact]
    public async Task Handle_WhenNoData_ReturnsZeroCountsAndHealthyStatus()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new GetHealthDashboardHandler(db);

        var result = await handler.Handle(new GetHealthDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ActiveMembers.Should().Be(0);
        result.Value.PendingPayments.Should().Be(0);
        result.Value.OpenTickets.Should().Be(0);
        result.Value.Status.Should().Be("healthy");
    }

    [Fact]
    public async Task Handle_CountsOnlyActiveMembers()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MemberProfiles.AddRangeAsync(
            BuildMember("AMB-001", MemberAccountStatus.Active),
            BuildMember("AMB-002", MemberAccountStatus.Active),
            BuildMember("AMB-003", MemberAccountStatus.Suspended));
        await db.SaveChangesAsync();

        var handler = new GetHealthDashboardHandler(db);
        var result = await handler.Handle(new GetHealthDashboardQuery(), CancellationToken.None);

        result.Value!.ActiveMembers.Should().Be(2);
    }

    [Fact]
    public async Task Handle_CountsOnlyPendingPayments()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.PaymentHistories.AddRangeAsync(
            BuildPayment(PaymentHistoryTransactionStatus.Pending),
            BuildPayment(PaymentHistoryTransactionStatus.Pending),
            BuildPayment(PaymentHistoryTransactionStatus.Captured),
            BuildPayment(PaymentHistoryTransactionStatus.Failed));
        await db.SaveChangesAsync();

        var handler = new GetHealthDashboardHandler(db);
        var result = await handler.Handle(new GetHealthDashboardQuery(), CancellationToken.None);

        result.Value!.PendingPayments.Should().Be(2);
    }

    [Fact]
    public async Task Handle_CountsOnlyOpenTickets()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.Tickets.AddRangeAsync(
            BuildTicket(TicketStatus.Open),
            BuildTicket(TicketStatus.Open),
            BuildTicket(TicketStatus.Resolved),
            BuildTicket(TicketStatus.InProgress));
        await db.SaveChangesAsync();

        var handler = new GetHealthDashboardHandler(db);
        var result = await handler.Handle(new GetHealthDashboardQuery(), CancellationToken.None);

        result.Value!.OpenTickets.Should().Be(2);
    }

    [Fact]
    public async Task Handle_AlwaysReturnsHealthyStatus()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new GetHealthDashboardHandler(db);

        var result = await handler.Handle(new GetHealthDashboardQuery(), CancellationToken.None);

        result.Value!.Status.Should().Be("healthy");
    }
}
