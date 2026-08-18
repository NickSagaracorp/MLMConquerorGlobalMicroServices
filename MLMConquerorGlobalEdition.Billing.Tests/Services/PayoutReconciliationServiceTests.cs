using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Payout;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using Xunit;
using MLMConquerorGlobalEdition.Repository.Services.Payout;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

public class PayoutReconciliationServiceTests
{
    private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Stale = Now.AddMinutes(-30); // older than the 15-min threshold
    private static readonly DateTime Fresh = Now.AddMinutes(-5);  // within the threshold

    private static PayoutReconciliationService CreateService(AppDbContext db, IPayoutGatewayService gateway)
        => CreateService(db, Result<IPayoutGatewayService>.Success(gateway));

    private static PayoutReconciliationService CreateService(AppDbContext db, Result<IPayoutGatewayService> resolved)
    {
        var resolverMock = new Mock<IPayoutGatewayResolver>();
        resolverMock.Setup(r => r.Resolve(It.IsAny<WalletType>())).Returns(resolved);

        var dt = new Mock<IDateTimeProvider>();
        dt.Setup(d => d.Now).Returns(Now);

        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns("recon");

        var receipts = new Mock<IPayoutReceiptService>();

        var orchestrator = new PayoutOrchestrator(db, resolverMock.Object, dt.Object, user.Object, receipts.Object);
        return new PayoutReconciliationService(db, resolverMock.Object, orchestrator, dt.Object);
    }

    private static Mock<IPayoutGatewayService> StatusGatewayMock(
        PayoutTransferState state, WalletType type = WalletType.eWallet)
    {
        var gw = new Mock<IPayoutGatewayService>();
        gw.Setup(g => g.GatewayType).Returns(type);
        gw.Setup(g => g.GetTransferStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(Result<PayoutTransferStatusResult>.Success(
              new PayoutTransferStatusResult { State = state, GatewayTransactionId = "recon-txn" }));
        return gw;
    }

    private static (PayoutAttempt attempt, CommissionEarning earning) SeedStaleAttempt(
        AppDbContext db, string memberId, decimal amount, DateTime attemptedAt,
        WalletType type = WalletType.eWallet, DisbursementMode mode = DisbursementMode.Online,
        string outcome = PayoutOutcome.Pending)
    {
        var earning = new CommissionEarning
        {
            BeneficiaryMemberId = memberId, Amount = amount, Status = CommissionEarningStatus.Pending,
            PaymentDate = Now.AddDays(-1), IsDeleted = false, CreationDate = Now, CreatedBy = "seed"
        };
        db.CommissionEarnings.Add(earning);
        db.SaveChanges();

        var attempt = new PayoutAttempt
        {
            MemberId = memberId, WalletTypeSnapshot = type, PayoutAccountSnapshot = "acct@x.com",
            AmountUsd = amount, ProcessDateUtc = Now, Outcome = outcome,
            AttemptedAtUtc = attemptedAt, EarningsCount = 1, DisbursementMode = mode,
            CreationDate = attemptedAt, CreatedBy = "seed"
        };
        db.PayoutAttempts.Add(attempt);
        db.SaveChanges();

        db.PayoutAttemptEarnings.Add(new PayoutAttemptEarning
        {
            PayoutAttemptId = attempt.Id, CommissionEarningId = earning.Id, Amount = amount,
            CreationDate = attemptedAt, CreatedBy = "seed"
        });
        db.SaveChanges();

        return (attempt, earning);
    }

    [Fact]
    public async Task Reconcile_StalePending_GatewaySucceeded_Finalizes_AndMarksEarningPaid()
    {
        using var db = TestDbContextFactory.Create();
        var (attempt, earning) = SeedStaleAttempt(db, "m1", 40m, Stale);

        var summary = await CreateService(db, StatusGatewayMock(PayoutTransferState.Succeeded).Object)
            .ReconcileStalePayoutsAsync();

        summary.Scanned.Should().Be(1);
        summary.Recovered.Should().Be(1);
        (await db.PayoutAttempts.SingleAsync()).Outcome.Should().Be(PayoutOutcome.Success);
        (await db.CommissionEarnings.SingleAsync(e => e.Id == earning.Id)).Status
            .Should().Be(CommissionEarningStatus.Paid);
    }

    [Fact]
    public async Task Reconcile_StalePending_GatewayFailed_MarksFailed_ReleasesEarning()
    {
        using var db = TestDbContextFactory.Create();
        var (_, earning) = SeedStaleAttempt(db, "m1", 40m, Stale);

        var summary = await CreateService(db, StatusGatewayMock(PayoutTransferState.Failed).Object)
            .ReconcileStalePayoutsAsync();

        summary.Released.Should().Be(1);
        (await db.PayoutAttempts.SingleAsync()).Outcome.Should().Be(PayoutOutcome.Failed);
        // Earning stays Pending; the reservation is released because the attempt is now Failed.
        (await db.CommissionEarnings.SingleAsync(e => e.Id == earning.Id)).Status
            .Should().Be(CommissionEarningStatus.Pending);
    }

    [Fact]
    public async Task Reconcile_StalePending_GatewayNotFound_MarksFailed_ReleasesEarning()
    {
        using var db = TestDbContextFactory.Create();
        SeedStaleAttempt(db, "m1", 40m, Stale);

        var summary = await CreateService(db, StatusGatewayMock(PayoutTransferState.NotFound).Object)
            .ReconcileStalePayoutsAsync();

        summary.Released.Should().Be(1);
        (await db.PayoutAttempts.SingleAsync()).Outcome.Should().Be(PayoutOutcome.Failed);
    }

    [Fact]
    public async Task Reconcile_StalePending_GatewayUnknown_LeavesPending()
    {
        using var db = TestDbContextFactory.Create();
        SeedStaleAttempt(db, "m1", 40m, Stale);

        var summary = await CreateService(db, StatusGatewayMock(PayoutTransferState.Unknown).Object)
            .ReconcileStalePayoutsAsync();

        summary.Unresolved.Should().Be(1);
        (await db.PayoutAttempts.SingleAsync()).Outcome.Should().Be(PayoutOutcome.Pending);
    }

    [Fact]
    public async Task Reconcile_RecentPending_WithinThreshold_NotTouched()
    {
        using var db = TestDbContextFactory.Create();
        SeedStaleAttempt(db, "m1", 40m, Fresh);

        var summary = await CreateService(db, StatusGatewayMock(PayoutTransferState.Succeeded).Object)
            .ReconcileStalePayoutsAsync();

        summary.Scanned.Should().Be(0);
        (await db.PayoutAttempts.SingleAsync()).Outcome.Should().Be(PayoutOutcome.Pending);
    }

    [Fact]
    public async Task Reconcile_CsvBulkPending_NotTouched()
    {
        using var db = TestDbContextFactory.Create();
        SeedStaleAttempt(db, "m1", 40m, Stale, mode: DisbursementMode.CsvBulk);

        var summary = await CreateService(db, StatusGatewayMock(PayoutTransferState.Succeeded).Object)
            .ReconcileStalePayoutsAsync();

        summary.Scanned.Should().Be(0);
        (await db.PayoutAttempts.SingleAsync()).Outcome.Should().Be(PayoutOutcome.Pending);
    }

    [Fact]
    public async Task Reconcile_AlreadySettledAttempts_Ignored()
    {
        using var db = TestDbContextFactory.Create();
        SeedStaleAttempt(db, "m1", 40m, Stale, outcome: PayoutOutcome.Success);
        SeedStaleAttempt(db, "m2", 20m, Stale, outcome: PayoutOutcome.Failed);

        var summary = await CreateService(db, StatusGatewayMock(PayoutTransferState.Succeeded).Object)
            .ReconcileStalePayoutsAsync();

        summary.Scanned.Should().Be(0);
    }

    [Fact]
    public async Task Reconcile_WhenNoGatewayConfigured_LeavesPending_AndCountsUnresolved()
    {
        using var db = TestDbContextFactory.Create();
        SeedStaleAttempt(db, "m1", 40m, Stale);

        var noGateway = Result<IPayoutGatewayService>.Failure("PAYOUT_GATEWAY_NOT_SUPPORTED", "none");
        var summary = await CreateService(db, noGateway).ReconcileStalePayoutsAsync();

        summary.Scanned.Should().Be(1);
        summary.Unresolved.Should().Be(1);
        (await db.PayoutAttempts.SingleAsync()).Outcome.Should().Be(PayoutOutcome.Pending);
    }
}
