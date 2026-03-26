using MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;
using MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.PayCommissions;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Commissions;

public class PayCommissionsHandlerTests
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
        EarnedDate = FixedNow.AddDays(-3),
        PaymentDate = FixedNow.AddDays(4),
        CreationDate = FixedNow.AddDays(-3),
        LastUpdateDate = FixedNow.AddDays(-3),
        CreatedBy = "seed"
    };

    [Fact]
    public async Task Handle_WhenNoIdsProvided_ReturnsNoIdsProvidedFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new PayCommissionsHandler(db, CurrentUser().Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new PayCommissionsCommand(new PayCommissionsRequest { CommissionIds = new List<string>() }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_IDS_PROVIDED");
    }

    [Fact]
    public async Task Handle_WhenOnlyPendingCommissions_PaysThemAndReturnsCount()
    {
        await using var db = InMemoryDbHelper.Create();
        var earning1 = BuildEarning(CommissionEarningStatus.Pending);
        var earning2 = BuildEarning(CommissionEarningStatus.Pending);
        await db.CommissionEarnings.AddRangeAsync(earning1, earning2);
        await db.SaveChangesAsync();

        var handler = new PayCommissionsHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        var result = await handler.Handle(
            new PayCommissionsCommand(new PayCommissionsRequest
            {
                CommissionIds = new List<string> { earning1.Id, earning2.Id }
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);

        db.CommissionEarnings.All(e => e.Status == CommissionEarningStatus.Paid).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenMixedStatuses_OnlyPaysPendingCommissions()
    {
        await using var db = InMemoryDbHelper.Create();
        var pending = BuildEarning(CommissionEarningStatus.Pending);
        var paid = BuildEarning(CommissionEarningStatus.Paid);
        var cancelled = BuildEarning(CommissionEarningStatus.Cancelled);
        await db.CommissionEarnings.AddRangeAsync(pending, paid, cancelled);
        await db.SaveChangesAsync();

        var handler = new PayCommissionsHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        var result = await handler.Handle(
            new PayCommissionsCommand(new PayCommissionsRequest
            {
                CommissionIds = new List<string> { pending.Id, paid.Id, cancelled.Id }
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenIdNotInDb_IgnoresItAndReturnsZero()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new PayCommissionsHandler(db, CurrentUser().Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new PayCommissionsCommand(new PayCommissionsRequest
            {
                CommissionIds = new List<string> { "does-not-exist" }
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenPaid_SetsPaymentDateToNow()
    {
        await using var db = InMemoryDbHelper.Create();
        var earning = BuildEarning(CommissionEarningStatus.Pending);
        await db.CommissionEarnings.AddAsync(earning);
        await db.SaveChangesAsync();

        var handler = new PayCommissionsHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        await handler.Handle(
            new PayCommissionsCommand(new PayCommissionsRequest
            {
                CommissionIds = new List<string> { earning.Id }
            }),
            CancellationToken.None);

        db.CommissionEarnings.Single().PaymentDate.Should().Be(FixedNow);
    }
}
