using FluentAssertions;
using Moq;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutAudit;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutAuditDetail;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.GetPayoutGatewayLog;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.VerifyPayoutReceipt;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payouts.ResendPayoutReceipt;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using Xunit;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Payouts;

public class PayoutAuditHandlerTests
{
    private static readonly DateTime BaseDate = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    private static PayoutAttempt SeedAttempt(AppDbContext db,
        string memberId = "AMB-1",
        WalletType wallet = WalletType.eWallet,
        string outcome = PayoutOutcome.Success,
        DateTime? attempted = null,
        bool withReceipt = false,
        bool withAnchor = false,
        string? gatewayErrorCode = null)
    {
        var a = new PayoutAttempt
        {
            MemberId = memberId,
            WalletTypeSnapshot = wallet,
            PayoutAccountSnapshot = "acct@test.com",
            AmountUsd = 100m,
            ProcessDateUtc = BaseDate,
            Outcome = outcome,
            AttemptedAtUtc = attempted ?? BaseDate,
            CompletedAtUtc = outcome == PayoutOutcome.Success ? BaseDate : null,
            EarningsCount = 1,
            GatewayErrorCode = gatewayErrorCode,
            ReceiptUrl = withReceipt ? "https://x/payout-receipts/f.pdf" : null,
            ReceiptSha256 = withReceipt ? "abc123" : null,
            ReceiptAnchorRef = withAnchor ? "sim-anchor:abc123" : null,
            CreationDate = BaseDate,
            CreatedBy = "seed"
        };
        db.PayoutAttempts.Add(a);
        db.SaveChanges();
        return a;
    }

    private static void SeedEarning(AppDbContext db, long attemptId, string earningId = "CE-1", decimal amount = 50m)
    {
        db.PayoutAttemptEarnings.Add(new PayoutAttemptEarning
        {
            PayoutAttemptId = attemptId,
            CommissionEarningId = earningId,
            Amount = amount,
            CreationDate = BaseDate,
            CreatedBy = "seed"
        });
        db.SaveChanges();
    }

    private static void SeedWalletLog(AppDbContext db, string memberId, WalletType walletType = WalletType.eWallet)
    {
        db.WalletApiLogs.Add(new MemberWalletApiLog
        {
            MemberId = memberId,
            WalletType = walletType,
            Operation = "Withdraw",
            HttpStatusCode = 200,
            Success = true,
            DurationMs = 150,
            CreationDate = BaseDate,
            CreatedBy = "seed"
        });
        db.SaveChanges();
    }

    // ── Test 1: Audit list filters by member, outcome and wallet type ────────

    [Fact]
    public async Task Audit_FiltersByMember_AndOutcome_AndWalletType()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedAttempt(db, "AMB-1", WalletType.eWallet, PayoutOutcome.Success);
        SeedAttempt(db, "AMB-2", WalletType.Volet, PayoutOutcome.Failed);
        SeedAttempt(db, "AMB-1", WalletType.eWallet, PayoutOutcome.Failed);

        // Filter: AMB-1 + eWallet + Success → should match only the first attempt
        var result = await new GetPayoutAuditHandler(db).Handle(
            new GetPayoutAuditQuery(
                From: null, To: null,
                MemberId: "AMB-1",
                WalletType: WalletType.eWallet,
                Outcome: PayoutOutcome.Success),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Single().MemberId.Should().Be("AMB-1");
        result.Value.Items.Single().WalletTypeSnapshot.Should().Be(WalletType.eWallet);
        result.Value.Items.Single().Outcome.Should().Be(PayoutOutcome.Success);
    }

    // ── Test 2: Audit list filters by date range ─────────────────────────────

    [Fact]
    public async Task Audit_FiltersByDateRange()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedAttempt(db, "AMB-1", attempted: BaseDate.AddDays(-5));  // outside range
        SeedAttempt(db, "AMB-2", attempted: BaseDate.AddDays(-1));  // inside range
        SeedAttempt(db, "AMB-3", attempted: BaseDate);              // inside range (boundary)
        SeedAttempt(db, "AMB-4", attempted: BaseDate.AddDays(1));   // outside range

        var from = BaseDate.AddDays(-2);
        var to = BaseDate.AddDays(1); // exclusive upper bound

        var result = await new GetPayoutAuditHandler(db).Handle(
            new GetPayoutAuditQuery(From: from, To: to, MemberId: null, WalletType: null, Outcome: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Select(x => x.MemberId).Should().BeEquivalentTo(new[] { "AMB-3", "AMB-2" });
    }

    // ── Test 3: Detail returns earnings ─────────────────────────────────────

    [Fact]
    public async Task Detail_ReturnsEarnings()
    {
        await using var db = InMemoryDbHelper.Create();
        var attempt = SeedAttempt(db);
        SeedEarning(db, attempt.Id, "CE-1", 60m);
        SeedEarning(db, attempt.Id, "CE-2", 40m);

        var result = await new GetPayoutAuditDetailHandler(db).Handle(
            new GetPayoutAuditDetailQuery(attempt.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PayoutAttemptId.Should().Be(attempt.Id);
        result.Value.MemberId.Should().Be("AMB-1");
        result.Value.DisbursementMode.Should().Be("Online");
        result.Value.Earnings.Should().HaveCount(2);
        result.Value.Earnings.Sum(e => e.Amount).Should().Be(100m);
    }

    // ── Test 4: Verify maps service result ──────────────────────────────────

    [Fact]
    public async Task Verify_MapsServiceResult()
    {
        await using var db = InMemoryDbHelper.Create();
        var attempt = SeedAttempt(db, withReceipt: true, withAnchor: true);

        var mockVerify = new Mock<IReceiptVerificationService>();
        mockVerify.Setup(v => v.VerifyAsync(It.IsAny<Domain.Entities.Billing.PayoutAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiptVerificationResult(
                HasReceipt: true,
                HashMatches: true,
                ChainValid: true,
                Anchored: true,
                AnchorRef: "sim-anchor:abc123",
                Detail: "Receipt authentic; chain link verified."));

        var result = await new VerifyPayoutReceiptHandler(db, mockVerify.Object).Handle(
            new VerifyPayoutReceiptCommand(attempt.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.HasReceipt.Should().BeTrue();
        result.Value.HashMatches.Should().BeTrue();
        result.Value.ChainValid.Should().BeTrue();
        result.Value.Anchored.Should().BeTrue();
        result.Value.AnchorRef.Should().Be("sim-anchor:abc123");
        result.Value.Detail.Should().Contain("authentic");
    }

    // ── Test 5: Resend fails for non-success outcome ─────────────────────────

    [Fact]
    public async Task Resend_WhenNotSuccess_Fails()
    {
        await using var db = InMemoryDbHelper.Create();
        SeedAttempt(db, outcome: PayoutOutcome.Failed);

        var attempt = db.PayoutAttempts.Single();
        var mockReceipts = new Mock<IPayoutReceiptService>();

        var result = await new ResendPayoutReceiptHandler(db, mockReceipts.Object).Handle(
            new ResendPayoutReceiptCommand(attempt.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PAYOUT_NOT_SUCCESSFUL");

        // Resend should not have been called
        mockReceipts.Verify(r => r.ResendReceiptAsync(It.IsAny<Domain.Entities.Billing.PayoutAttempt>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
