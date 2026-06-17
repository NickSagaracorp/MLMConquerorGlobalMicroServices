using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Payout;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Csv;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Payout;

public class PayoutBatchReconciliationServiceTests
{
    private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    // A fake finalizer that performs the real DB effect (mark earnings Paid + attempt Success).
    private static Mock<IPayoutOrchestrator> FinalizerFor(AppDbContext db)
    {
        var orch = new Mock<IPayoutOrchestrator>();
        orch.Setup(o => o.FinalizeSuccessAsync(
                It.IsAny<PayoutAttempt>(),
                It.IsAny<string?>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (PayoutAttempt a, string? txn, long? lat, CancellationToken c) =>
            {
                var ids = await db.PayoutAttemptEarnings
                    .Where(x => x.PayoutAttemptId == a.Id)
                    .Select(x => x.CommissionEarningId)
                    .ToListAsync(c);
                foreach (var e in await db.CommissionEarnings.Where(e => ids.Contains(e.Id)).ToListAsync(c))
                    e.Status = CommissionEarningStatus.Paid;
                a.Outcome = PayoutOutcome.Success;
                a.GatewayTransactionId = txn;
                a.CompletedAtUtc = Now;
                await db.SaveChangesAsync(c);
            });
        return orch;
    }

    private static PayoutBatchReconciliationService Build(AppDbContext db, Mock<IPayoutOrchestrator>? orch = null)
    {
        var dt = new Mock<IDateTimeProvider>();
        dt.Setup(d => d.Now).Returns(Now);

        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns("admin-1");

        var resolver = new PayoutCsvResolver(
            new IPayoutCsvFormatter[] { new EWalletPayoutCsvAdapter(), new VoletPayoutCsvAdapter() },
            new IPayoutResultCsvParser[] { new EWalletPayoutCsvAdapter(), new VoletPayoutCsvAdapter() });

        var storage = new Mock<IReceiptStorage>();
        storage.Setup(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync("url");

        return new PayoutBatchReconciliationService(
            db, resolver, (orch ?? FinalizerFor(db)).Object, storage.Object, dt.Object, user.Object);
    }

    private static (PayoutBatch batch, PayoutAttempt a1, PayoutAttempt a2) SeedBatch(AppDbContext db)
    {
        var batch = new PayoutBatch
        {
            WalletType = WalletType.eWallet,
            ProcessDateUtc = Now,
            Status = PayoutBatchStatus.Exported,
            MemberCount = 2,
            TotalAmountUsd = 80m,
            CreationDate = Now,
            CreatedBy = "s",
            LastUpdateDate = Now,
            LastUpdateBy = "s"
        };
        db.PayoutBatches.Add(batch);
        db.SaveChanges();

        PayoutAttempt Mk(string m, decimal amt)
        {
            var a = new PayoutAttempt
            {
                MemberId = m,
                WalletTypeSnapshot = WalletType.eWallet,
                PayoutAccountSnapshot = m,
                AmountUsd = amt,
                ProcessDateUtc = Now,
                Outcome = PayoutOutcome.Pending,
                AttemptedAtUtc = Now,
                EarningsCount = 1,
                DisbursementMode = DisbursementMode.CsvBulk,
                PayoutBatchId = batch.Id,
                CreationDate = Now,
                CreatedBy = "s"
            };
            db.PayoutAttempts.Add(a);
            db.SaveChanges();

            var e = new CommissionEarning
            {
                BeneficiaryMemberId = m,
                Amount = amt,
                Status = CommissionEarningStatus.Pending,
                PaymentDate = Now.AddDays(-1),
                IsDeleted = false,
                CreationDate = Now,
                CreatedBy = "s"
            };
            db.CommissionEarnings.Add(e);
            db.SaveChanges();

            db.PayoutAttemptEarnings.Add(new PayoutAttemptEarning
            {
                PayoutAttemptId = a.Id,
                CommissionEarningId = e.Id,
                Amount = amt,
                CreationDate = Now,
                CreatedBy = "s"
            });
            db.SaveChanges();

            return a;
        }

        return (batch, Mk("AMB-1", 50m), Mk("AMB-2", 30m));
    }

    [Fact]
    public async Task Reconcile_SuccessRow_Paid_FailRow_ReleasedToPending_PartialStatus()
    {
        using var db = TestDbContextFactory.Create();
        var (batch, a1, a2) = SeedBatch(db);
        var csv = $"Reference,Status,TransactionId,ErrorCode,ErrorMessage\n{a1.Id},SUCCESS,txn-1,,\n{a2.Id},FAILED,,E5,Closed\n";

        var result = await Build(db).ReconcileFromResultsAsync(batch.Id, csv);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Succeeded.Should().Be(1);
        result.Value.Failed.Should().Be(1);
        result.Value.BatchStatus.Should().Be(PayoutBatchStatus.PartiallyReconciled);
        (await db.PayoutAttempts.FindAsync(a1.Id))!.Outcome.Should().Be(PayoutOutcome.Success);
        (await db.PayoutAttempts.FindAsync(a2.Id))!.Outcome.Should().Be(PayoutOutcome.Failed);
        // the failed attempt's earnings are freed (still Pending); the success attempt's earnings are Paid
        (await db.CommissionEarnings.CountAsync(e => e.Status == CommissionEarningStatus.Pending)).Should().Be(1);
    }

    [Fact]
    public async Task Reconcile_AllSuccess_StatusReconciled()
    {
        using var db = TestDbContextFactory.Create();
        var (batch, a1, a2) = SeedBatch(db);
        var csv = $"Reference,Status,TransactionId,ErrorCode,ErrorMessage\n{a1.Id},SUCCESS,t1,,\n{a2.Id},SUCCESS,t2,,\n";

        var result = await Build(db).ReconcileFromResultsAsync(batch.Id, csv);

        result.IsSuccess.Should().BeTrue();
        result.Value!.BatchStatus.Should().Be(PayoutBatchStatus.Reconciled);
    }

    [Fact]
    public async Task MarkBatchPaid_MarksAllPendingPaid_Reconciled()
    {
        using var db = TestDbContextFactory.Create();
        var (batch, a1, a2) = SeedBatch(db);

        var result = await Build(db).MarkBatchPaidAsync(batch.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Succeeded.Should().Be(2);
        result.Value.BatchStatus.Should().Be(PayoutBatchStatus.Reconciled);
        (await db.CommissionEarnings.CountAsync(e => e.Status == CommissionEarningStatus.Paid)).Should().Be(2);
    }

    [Fact]
    public async Task CancelBatch_ReleasesReservations_Cancelled()
    {
        using var db = TestDbContextFactory.Create();
        var (batch, a1, a2) = SeedBatch(db);

        var result = await Build(db).CancelBatchAsync(batch.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.BatchStatus.Should().Be(PayoutBatchStatus.Cancelled);
        (await db.PayoutAttempts.CountAsync(a => a.Outcome == PayoutOutcome.Failed)).Should().Be(2);
        // both attempts are Failed → reservation guard excludes them → earnings stay Pending (freed)
        (await db.CommissionEarnings.CountAsync(e => e.Status == CommissionEarningStatus.Pending)).Should().Be(2);
    }

    [Fact]
    public async Task Reconcile_UnknownBatch_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var result = await Build(db).ReconcileFromResultsAsync("nope", "Reference,Status\n");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PAYOUT_BATCH_NOT_FOUND");
    }

    [Fact]
    public async Task Reconcile_WhenOneRowFinalizeThrows_OtherRowsStillProcess()
    {
        using var db = TestDbContextFactory.Create();
        var (batch, a1, a2) = SeedBatch(db);

        // a1 finalize throws; a2 finalize succeeds (performs real DB effect).
        var throwingOrch = new Mock<IPayoutOrchestrator>();
        var callCount = 0;
        throwingOrch.Setup(o => o.FinalizeSuccessAsync(
                It.IsAny<PayoutAttempt>(),
                It.IsAny<string?>(),
                It.IsAny<long?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (PayoutAttempt a, string? txn, long? lat, CancellationToken c) =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("Simulated transient DB error");

                // Second call succeeds: actually update DB.
                var ids = await db.PayoutAttemptEarnings
                    .Where(x => x.PayoutAttemptId == a.Id)
                    .Select(x => x.CommissionEarningId)
                    .ToListAsync(c);
                foreach (var e in await db.CommissionEarnings.Where(e => ids.Contains(e.Id)).ToListAsync(c))
                    e.Status = CommissionEarningStatus.Paid;
                a.Outcome = PayoutOutcome.Success;
                a.GatewayTransactionId = txn;
                a.CompletedAtUtc = Now;
                await db.SaveChangesAsync(c);
            });

        var csv = $"Reference,Status,TransactionId,ErrorCode,ErrorMessage\n{a1.Id},SUCCESS,txn-1,,\n{a2.Id},SUCCESS,txn-2,,\n";
        var result = await Build(db, throwingOrch).ReconcileFromResultsAsync(batch.Id, csv);

        result.IsSuccess.Should().BeTrue();
        // One threw (counted as failed), one succeeded
        result.Value!.Succeeded.Should().Be(1);
        result.Value.Failed.Should().Be(1);
        result.Value.BatchStatus.Should().Be(PayoutBatchStatus.PartiallyReconciled);

        // The row that threw should be marked Failed so earnings are freed
        (await db.PayoutAttempts.FindAsync(a1.Id))!.Outcome.Should().Be(PayoutOutcome.Failed);
        // The other row should have been processed successfully
        (await db.PayoutAttempts.FindAsync(a2.Id))!.Outcome.Should().Be(PayoutOutcome.Success);
    }
}
