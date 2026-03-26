using MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.CancelCommission;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Commissions;

public class CancelCommissionHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<ICurrentUserService> CurrentUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("admin-001");
        return m;
    }

    private static Mock<IDateTimeProvider> DateTimeProvider()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static CommissionEarning BuildEarning(CommissionEarningStatus status) => new()
    {
        BeneficiaryMemberId = "AMB-001",
        CommissionTypeId = 1,
        Amount = 50m,
        Status = status,
        EarnedDate = FixedNow.AddDays(-5),
        PaymentDate = FixedNow.AddDays(2),
        CreationDate = FixedNow.AddDays(-5),
        LastUpdateDate = FixedNow.AddDays(-5),
        CreatedBy = "seed"
    };

    [Fact]
    public async Task Handle_WhenCommissionNotFound_ReturnsCommissionNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new CancelCommissionHandler(db, CurrentUser().Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new CancelCommissionCommand("nonexistent-id"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("COMMISSION_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenCommissionIsPaid_ReturnsCommissionAlreadyPaidFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var earning = BuildEarning(CommissionEarningStatus.Paid);
        await db.CommissionEarnings.AddAsync(earning);
        await db.SaveChangesAsync();

        var handler = new CancelCommissionHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        var result = await handler.Handle(
            new CancelCommissionCommand(earning.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("COMMISSION_ALREADY_PAID");
    }

    [Fact]
    public async Task Handle_WhenCommissionIsAlreadyCancelled_ReturnsCommissionAlreadyCancelledFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var earning = BuildEarning(CommissionEarningStatus.Cancelled);
        await db.CommissionEarnings.AddAsync(earning);
        await db.SaveChangesAsync();

        var handler = new CancelCommissionHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        var result = await handler.Handle(
            new CancelCommissionCommand(earning.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("COMMISSION_ALREADY_CANCELLED");
    }

    [Fact]
    public async Task Handle_WhenCommissionIsPending_CancelsIt()
    {
        await using var db = InMemoryDbHelper.Create();
        var earning = BuildEarning(CommissionEarningStatus.Pending);
        await db.CommissionEarnings.AddAsync(earning);
        await db.SaveChangesAsync();

        var handler = new CancelCommissionHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        var result = await handler.Handle(
            new CancelCommissionCommand(earning.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        var updated = db.CommissionEarnings.Single();
        updated.Status.Should().Be(CommissionEarningStatus.Cancelled);
    }
}
