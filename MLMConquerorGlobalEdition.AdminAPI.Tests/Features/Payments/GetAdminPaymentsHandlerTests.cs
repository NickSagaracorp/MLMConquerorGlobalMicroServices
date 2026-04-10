using MLMConquerorGlobalEdition.AdminAPI.Features.Payments.GetAdminPayments;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Payments;

public class GetAdminPaymentsHandlerTests
{
    private static PagedRequest Page(int page = 1, int size = 10) => new() { Page = page, PageSize = size };

    private static PaymentHistory BuildPayment(
        string id, string memberId = "AMB-001",
        PaymentHistoryTransactionStatus status = PaymentHistoryTransactionStatus.Captured,
        string orderId = "ORD-001")
    {
        return new PaymentHistory
        {
            Id = id,
            MemberId = memberId,
            OrderId = orderId,
            Amount = 100m,
            GatewayName = "Stripe",
            TransactionStatus = status,
            CreationDate = DateTime.UtcNow,
            CreatedBy = "system",
            LastUpdateDate = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task Handle_WhenNoPayments_ReturnsEmptyPagedResult()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new GetAdminPaymentsHandler(db);

        var result = await handler.Handle(
            new GetAdminPaymentsQuery(Page(), null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenPaymentsExist_ReturnsMappedDtos()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.PaymentHistories.AddAsync(BuildPayment("PH-001"));
        await db.SaveChangesAsync();

        var handler = new GetAdminPaymentsHandler(db);

        var result = await handler.Handle(
            new GetAdminPaymentsQuery(Page(), null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        var dto = result.Value.Items.Single();
        dto.MemberId.Should().Be("AMB-001");
        dto.GatewayName.Should().Be("Stripe");
        dto.Amount.Should().Be(100m);
        dto.Status.Should().Be("Captured");
    }

    [Fact]
    public async Task Handle_WhenMemberIdFilter_ReturnsOnlyThatMembersPayments()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.PaymentHistories.AddRangeAsync(
            BuildPayment("PH-001", "AMB-001"),
            BuildPayment("PH-002", "AMB-002"),
            BuildPayment("PH-003", "AMB-001")
        );
        await db.SaveChangesAsync();

        var handler = new GetAdminPaymentsHandler(db);

        var result = await handler.Handle(
            new GetAdminPaymentsQuery(Page(), null, "AMB-001"), CancellationToken.None);

        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Should().AllSatisfy(dto => dto.MemberId.Should().Be("AMB-001"));
    }

    [Fact]
    public async Task Handle_WhenStatusFilter_ReturnsOnlyMatchingStatus()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.PaymentHistories.AddRangeAsync(
            BuildPayment("PH-001", status: PaymentHistoryTransactionStatus.Captured),
            BuildPayment("PH-002", status: PaymentHistoryTransactionStatus.Failed),
            BuildPayment("PH-003", status: PaymentHistoryTransactionStatus.Pending)
        );
        await db.SaveChangesAsync();

        var handler = new GetAdminPaymentsHandler(db);

        var result = await handler.Handle(
            new GetAdminPaymentsQuery(Page(), "Failed", null), CancellationToken.None);

        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Single().Status.Should().Be("Failed");
    }

    [Fact]
    public async Task Handle_WhenBothFiltersApplied_IntersectsCorrectly()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.PaymentHistories.AddRangeAsync(
            BuildPayment("PH-001", "AMB-001", PaymentHistoryTransactionStatus.Failed),
            BuildPayment("PH-002", "AMB-002", PaymentHistoryTransactionStatus.Failed),
            BuildPayment("PH-003", "AMB-001", PaymentHistoryTransactionStatus.Captured)
        );
        await db.SaveChangesAsync();

        var handler = new GetAdminPaymentsHandler(db);

        var result = await handler.Handle(
            new GetAdminPaymentsQuery(Page(), "Failed", "AMB-001"), CancellationToken.None);

        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Single().Id.Should().Be("PH-001");
    }

    [Fact]
    public async Task Handle_ReturnsPaginatedSubset()
    {
        await using var db = InMemoryDbHelper.Create();
        for (int i = 0; i < 6; i++)
            await db.PaymentHistories.AddAsync(BuildPayment($"PH-{i:D3}"));
        await db.SaveChangesAsync();

        var handler = new GetAdminPaymentsHandler(db);

        var result = await handler.Handle(
            new GetAdminPaymentsQuery(Page(page: 2, size: 4), null, null), CancellationToken.None);

        result.Value!.TotalCount.Should().Be(6);
        result.Value.Items.Count().Should().Be(2);
        result.Value.Page.Should().Be(2);
    }
}
