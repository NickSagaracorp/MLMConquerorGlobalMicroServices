using FluentAssertions;
using Moq;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.CancelPayoutBatch;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ExportPayoutBatch;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutBatchDetail;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutBatches;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.MarkPayoutBatchPaid;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ReconcilePayoutBatch;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;
using Xunit;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Payouts;

public class PayoutBatchHandlerTests
{
    private static readonly DateTime BaseDate = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    private static PayoutBatch SeedBatch(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db,
        string status = PayoutBatchStatus.Exported,
        WalletType walletType = WalletType.eWallet)
    {
        var batch = new PayoutBatch
        {
            WalletType = walletType,
            ProcessDateUtc = BaseDate,
            Status = status,
            MemberCount = 1,
            TotalAmountUsd = 50m,
            CreationDate = BaseDate,
            CreatedBy = "seed",
            LastUpdateDate = BaseDate,
            LastUpdateBy = "seed"
        };
        db.PayoutBatches.Add(batch);
        db.SaveChanges();
        return batch;
    }

    private static PayoutAttempt SeedAttempt(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db,
        string batchId,
        string memberId = "AMB-1",
        decimal amount = 50m,
        string outcome = PayoutOutcome.Pending)
    {
        var attempt = new PayoutAttempt
        {
            MemberId = memberId,
            WalletTypeSnapshot = WalletType.eWallet,
            PayoutAccountSnapshot = memberId + "@x.com",
            AmountUsd = amount,
            ProcessDateUtc = BaseDate,
            Outcome = outcome,
            AttemptedAtUtc = BaseDate,
            EarningsCount = 1,
            DisbursementMode = DisbursementMode.CsvBulk,
            PayoutBatchId = batchId,
            CreationDate = BaseDate,
            CreatedBy = "seed"
        };
        db.PayoutAttempts.Add(attempt);
        db.SaveChanges();
        return attempt;
    }

    // ── Test 1: GetPayoutBatches — returns all batches ordered newest first ──

    [Fact]
    public async Task GetBatches_ReturnsPaged_OrderedNewestFirst()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedBatch(db, PayoutBatchStatus.Exported, WalletType.eWallet);
        SeedBatch(db, PayoutBatchStatus.Reconciled, WalletType.Volet);

        var result = await new GetPayoutBatchesHandler(db).Handle(
            new GetPayoutBatchesQuery(Status: null, WalletType: null, Page: 1, PageSize: 20),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
    }

    // ── Test 2: GetPayoutBatches — filters by status ────────────────────────

    [Fact]
    public async Task GetBatches_FiltersByStatus()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedBatch(db, PayoutBatchStatus.Exported);
        SeedBatch(db, PayoutBatchStatus.Reconciled);
        SeedBatch(db, PayoutBatchStatus.Cancelled);

        var result = await new GetPayoutBatchesHandler(db).Handle(
            new GetPayoutBatchesQuery(Status: PayoutBatchStatus.Reconciled, WalletType: null, Page: 1, PageSize: 20),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Single().Status.Should().Be(PayoutBatchStatus.Reconciled);
    }

    // ── Test 3: GetPayoutBatchDetail — returns batch + member attempts ───────

    [Fact]
    public async Task GetBatchDetail_ReturnsMembersAndBatchInfo()
    {
        await using var db = InMemoryDbHelper.Create();
        var batch = SeedBatch(db);
        SeedAttempt(db, batch.Id, "AMB-1", 50m);
        SeedAttempt(db, batch.Id, "AMB-2", 30m);

        var result = await new GetPayoutBatchDetailHandler(db).Handle(
            new GetPayoutBatchDetailQuery(batch.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(batch.Id);
        result.Value.Status.Should().Be(PayoutBatchStatus.Exported);
        result.Value.Members.Should().HaveCount(2);
        result.Value.Members.Select(m => m.MemberId).Should().BeEquivalentTo(new[] { "AMB-1", "AMB-2" });
    }

    // ── Test 4: GetPayoutBatchDetail — NOT_FOUND when batch does not exist ───

    [Fact]
    public async Task GetBatchDetail_WhenBatchNotFound_ReturnsFailure()
    {
        await using var db = InMemoryDbHelper.Create();

        var result = await new GetPayoutBatchDetailHandler(db).Handle(
            new GetPayoutBatchDetailQuery("batch-does-not-exist"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PAYOUT_BATCH_NOT_FOUND");
    }

    // ── Test 5: ExportPayoutBatch — delegates to IPayoutBatchExportService ───

    [Fact]
    public async Task ExportBatch_DelegatesToExportService_ReturnsResult()
    {
        var expectedResult = new PayoutBatchExportResult(
            BatchId: "batch-123",
            MemberCount: 2,
            TotalAmountUsd: 80m,
            CsvBytes: System.Text.Encoding.UTF8.GetBytes("Reference,Account,Amount,Currency\r\n"),
            FileName: "payout-batch-batch-123.csv");

        var mockExport = new Mock<IPayoutBatchExportService>();
        mockExport.Setup(s => s.ExportAsync(WalletType.eWallet, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<PayoutBatchExportResult>.Success(expectedResult));

        var result = await new ExportPayoutBatchHandler(mockExport.Object).Handle(
            new ExportPayoutBatchCommand(WalletType.eWallet, BaseDate),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.BatchId.Should().Be("batch-123");
        result.Value.MemberCount.Should().Be(2);
        result.Value.TotalAmountUsd.Should().Be(80m);
        result.Value.FileName.Should().Be("payout-batch-batch-123.csv");
    }

    // ── Test 6: ReconcilePayoutBatch — delegates to reconciliation service ───

    [Fact]
    public async Task ReconcileHandler_DelegatesToReconciliationService()
    {
        var mockRecon = new Mock<IPayoutBatchReconciliationService>();
        mockRecon.Setup(s => s.ReconcileFromResultsAsync("batch-1", "csv-content", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<BatchReconcileResult>.Success(
                     new BatchReconcileResult(2, 0, PayoutBatchStatus.Reconciled)));

        var result = await new ReconcilePayoutBatchHandler(mockRecon.Object).Handle(
            new ReconcilePayoutBatchCommand("batch-1", "csv-content"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Succeeded.Should().Be(2);
        result.Value.BatchStatus.Should().Be(PayoutBatchStatus.Reconciled);
    }

    // ── Test 7: MarkPayoutBatchPaid — delegates to reconciliation service ────

    [Fact]
    public async Task MarkPaidHandler_DelegatesToReconciliationService()
    {
        var mockRecon = new Mock<IPayoutBatchReconciliationService>();
        mockRecon.Setup(s => s.MarkBatchPaidAsync("batch-2", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<BatchReconcileResult>.Success(
                     new BatchReconcileResult(3, 0, PayoutBatchStatus.Reconciled)));

        var result = await new MarkPayoutBatchPaidHandler(mockRecon.Object).Handle(
            new MarkPayoutBatchPaidCommand("batch-2"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Succeeded.Should().Be(3);
        result.Value.BatchStatus.Should().Be(PayoutBatchStatus.Reconciled);
    }

    // ── Test 8: CancelPayoutBatch — delegates to reconciliation service ──────

    [Fact]
    public async Task CancelHandler_DelegatesToReconciliationService()
    {
        var mockRecon = new Mock<IPayoutBatchReconciliationService>();
        mockRecon.Setup(s => s.CancelBatchAsync("batch-3", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Result<BatchReconcileResult>.Success(
                     new BatchReconcileResult(0, 2, PayoutBatchStatus.Cancelled)));

        var result = await new CancelPayoutBatchHandler(mockRecon.Object).Handle(
            new CancelPayoutBatchCommand("batch-3"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Failed.Should().Be(2);
        result.Value.BatchStatus.Should().Be(PayoutBatchStatus.Cancelled);
    }

    // ── Test 9: GetPayoutBatches — filters by wallet type ───────────────────

    [Fact]
    public async Task GetBatches_FiltersByWalletType()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedBatch(db, PayoutBatchStatus.Exported, WalletType.eWallet);
        SeedBatch(db, PayoutBatchStatus.Exported, WalletType.Volet);
        SeedBatch(db, PayoutBatchStatus.Reconciled, WalletType.eWallet);

        var result = await new GetPayoutBatchesHandler(db).Handle(
            new GetPayoutBatchesQuery(Status: null, WalletType: WalletType.eWallet, Page: 1, PageSize: 20),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Should().AllSatisfy(b => b.WalletType.Should().Be(WalletType.eWallet));
    }
}
