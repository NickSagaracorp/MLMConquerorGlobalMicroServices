using MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;
using MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.CreateCommission;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Commissions;

public class CreateCommissionHandlerTests
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

    private static async Task SeedCommissionType(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db,
        int id, int paymentDelayDays = 7)
    {
        await db.CommissionTypes.AddAsync(new CommissionType
        {
            Id = id,
            Name = "Manual Bonus",
            IsActive = true,
            IsSponsorBonus = false,
            LevelNo = 1,
            PaymentDelayDays = paymentDelayDays,
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WhenCommissionTypeNotFound_ReturnsCommissionTypeNotFoundFailure()
    {
        await using var db = InMemoryDbHelper.Create();
        var handler = new CreateCommissionHandler(db, CurrentUser().Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new CreateCommissionCommand(new CreateCommissionRequest
            {
                BeneficiaryMemberId = "AMB-001",
                CommissionTypeId = 999,
                Amount = 50m
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("COMMISSION_TYPE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenCommissionTypeExists_CreatesEarningWithIsManualEntryTrue()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedCommissionType(db, 1, paymentDelayDays: 7);
        var handler = new CreateCommissionHandler(db, CurrentUser().Object, DateTimeProvider().Object);

        await handler.Handle(
            new CreateCommissionCommand(new CreateCommissionRequest
            {
                BeneficiaryMemberId = "AMB-001",
                CommissionTypeId = 1,
                Amount = 100m
            }),
            CancellationToken.None);

        var earning = db.CommissionEarnings.Single();
        earning.IsManualEntry.Should().BeTrue();
        earning.BeneficiaryMemberId.Should().Be("AMB-001");
        earning.Amount.Should().Be(100m);
        earning.Status.Should().Be(CommissionEarningStatus.Pending);
    }

    [Fact]
    public async Task Handle_WhenCreated_PaymentDateIsEarnedDatePlusPaymentDelay()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedCommissionType(db, 1, paymentDelayDays: 14);
        var handler = new CreateCommissionHandler(db, CurrentUser().Object, DateTimeProvider().Object);

        await handler.Handle(
            new CreateCommissionCommand(new CreateCommissionRequest
            {
                BeneficiaryMemberId = "AMB-001",
                CommissionTypeId = 1,
                Amount = 50m
            }),
            CancellationToken.None);

        var earning = db.CommissionEarnings.Single();
        earning.PaymentDate.Should().Be(FixedNow.AddDays(14));
        earning.EarnedDate.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Handle_WhenCreated_ReturnsDtoWithIsManualEntryTrue()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedCommissionType(db, 1);
        var handler = new CreateCommissionHandler(db, CurrentUser().Object, DateTimeProvider().Object);

        var result = await handler.Handle(
            new CreateCommissionCommand(new CreateCommissionRequest
            {
                BeneficiaryMemberId = "AMB-001",
                CommissionTypeId = 1,
                Amount = 75m,
                Notes = "Bonus for achievement"
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsManualEntry.Should().BeTrue();
        result.Value.Amount.Should().Be(75m);
        result.Value.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Handle_WhenPeriodDateProvided_SetsPeriodDateOnEarning()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedCommissionType(db, 1);
        var handler = new CreateCommissionHandler(db, CurrentUser().Object, DateTimeProvider().Object);
        var period = new DateTime(2026, 3, 1);

        await handler.Handle(
            new CreateCommissionCommand(new CreateCommissionRequest
            {
                BeneficiaryMemberId = "AMB-001",
                CommissionTypeId = 1,
                Amount = 50m,
                PeriodDate = period
            }),
            CancellationToken.None);

        db.CommissionEarnings.Single().PeriodDate.Should().Be(period);
    }
}
