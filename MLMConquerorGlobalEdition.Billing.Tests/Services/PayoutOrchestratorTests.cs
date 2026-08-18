using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.Payout;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.SharedKernel;
using Xunit;
using MLMConquerorGlobalEdition.Repository.Services.Payout;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

public class PayoutOrchestratorTests
{
    private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    private static PayoutOrchestrator Create(
        AppDbContext db,
        IPayoutGatewayService gateway)
    {
        var resolverMock = new Mock<IPayoutGatewayResolver>();
        resolverMock.Setup(r => r.Resolve(It.IsAny<WalletType>()))
                    .Returns(Result<IPayoutGatewayService>.Success(gateway));

        var dt = new Mock<IDateTimeProvider>();
        dt.Setup(d => d.Now).Returns(Now);

        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns("admin-1");

        var receipts = new Mock<IPayoutReceiptService>();
        // IssueReceiptAsync returns Task.CompletedTask by default (no setup needed)

        return new PayoutOrchestrator(db, resolverMock.Object, dt.Object, user.Object, receipts.Object);
    }

    private static void SeedWallet(AppDbContext db, string memberId, WalletType type, string account, bool preferred = true)
    {
        db.Wallets.Add(new MemberProfilesWallet
        {
            MemberId = memberId, WalletType = type, Status = WalletStatus.Approved,
            AccountIdentifier = account, IsPreferred = preferred, IsDeleted = false,
            CreationDate = Now, CreatedBy = "seed", LastUpdateDate = Now
        });
    }

    private static CommissionEarning SeedEarning(AppDbContext db, string memberId, decimal amount, DateTime paymentDate)
    {
        var e = new CommissionEarning
        {
            BeneficiaryMemberId = memberId, Amount = amount, Status = CommissionEarningStatus.Pending,
            PaymentDate = paymentDate, IsDeleted = false, CreationDate = Now, CreatedBy = "seed"
        };
        db.CommissionEarnings.Add(e);
        return e;
    }

    private static Mock<IPayoutGatewayService> GatewayMock(WalletType type = WalletType.eWallet)
    {
        var gw = new Mock<IPayoutGatewayService>();
        gw.Setup(g => g.GatewayType).Returns(type);
        gw.Setup(g => g.ValidateAccountAsync(It.IsAny<PayoutAccountContext>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(Result<PayoutAccountResult>.Success(new PayoutAccountResult { Exists = true }));
        gw.Setup(g => g.SubscribeAccountAsync(It.IsAny<PayoutAccountContext>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(Result<PayoutAccountResult>.Success(new PayoutAccountResult { Exists = true }));
        gw.Setup(g => g.DisburseAsync(It.IsAny<PayoutTransferContext>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(Result<PayoutTransferResult>.Success(new PayoutTransferResult { GatewayTransactionId = "txn-1" }));
        return gw;
    }

    [Fact]
    public async Task Payout_WhenGatewayConfirms_MarksEarningsPaid()
    {
        using var db = TestDbContextFactory.Create();
        SeedWallet(db, "m1", WalletType.eWallet, "wallet@x.com");
        SeedEarning(db, "m1", 30m, Now.AddDays(-1));
        SeedEarning(db, "m1", 20m, Now.AddDays(-1));
        await db.SaveChangesAsync();

        var result = await Create(db, GatewayMock().Object).ExecutePayoutAsync("m1", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Outcome.Should().Be(PayoutOutcome.Success);
        result.Value.AmountUsd.Should().Be(50m);
        (await db.CommissionEarnings.CountAsync(e => e.Status == CommissionEarningStatus.Paid)).Should().Be(2);
    }

    [Fact]
    public async Task Payout_WhenGatewayRejects_LeavesEarningsPending_AndAuditsError()
    {
        using var db = TestDbContextFactory.Create();
        SeedWallet(db, "m1", WalletType.eWallet, "wallet@x.com");
        SeedEarning(db, "m1", 40m, Now.AddDays(-1));
        await db.SaveChangesAsync();

        var gw = GatewayMock();
        gw.Setup(g => g.DisburseAsync(It.IsAny<PayoutTransferContext>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(Result<PayoutTransferResult>.Failure("E_DECLINED", "Gateway declined"));

        var result = await Create(db, gw.Object).ExecutePayoutAsync("m1", Now);

        result.Value!.Outcome.Should().Be(PayoutOutcome.Failed);
        result.Value.GatewayErrorCode.Should().Be("E_DECLINED");
        (await db.CommissionEarnings.CountAsync(e => e.Status == CommissionEarningStatus.Pending)).Should().Be(1);
        var attempt = await db.PayoutAttempts.SingleAsync();
        attempt.Outcome.Should().Be(PayoutOutcome.Failed);
        attempt.GatewayErrorMessage.Should().Be("Gateway declined");
    }

    [Fact]
    public async Task Payout_WhenAccountMissing_AutoSubscribesThenTransfers()
    {
        using var db = TestDbContextFactory.Create();
        SeedWallet(db, "m1", WalletType.eWallet, "wallet@x.com");
        SeedEarning(db, "m1", 25m, Now.AddDays(-1));
        await db.SaveChangesAsync();

        var gw = GatewayMock();
        gw.Setup(g => g.ValidateAccountAsync(It.IsAny<PayoutAccountContext>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(Result<PayoutAccountResult>.Success(new PayoutAccountResult { Exists = false }));

        var result = await Create(db, gw.Object).ExecutePayoutAsync("m1", Now);

        result.Value!.Outcome.Should().Be(PayoutOutcome.Success);
        gw.Verify(g => g.SubscribeAccountAsync(It.IsAny<PayoutAccountContext>(), It.IsAny<CancellationToken>()), Times.Once);
        gw.Verify(g => g.DisburseAsync(It.IsAny<PayoutTransferContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Payout_SnapshotsGatewayAndAccount_AtPaymentTime()
    {
        using var db = TestDbContextFactory.Create();
        SeedWallet(db, "m1", WalletType.Volet, "volet-acct-123");
        SeedEarning(db, "m1", 25m, Now.AddDays(-1));
        await db.SaveChangesAsync();

        await Create(db, GatewayMock(WalletType.Volet).Object).ExecutePayoutAsync("m1", Now);

        var attempt = await db.PayoutAttempts.SingleAsync();
        attempt.WalletTypeSnapshot.Should().Be(WalletType.Volet);
        attempt.PayoutAccountSnapshot.Should().Be("volet-acct-123");
    }

    [Fact]
    public async Task Payout_WhenAccountChangesLater_AuditSnapshotUnchanged()
    {
        using var db = TestDbContextFactory.Create();
        SeedWallet(db, "m1", WalletType.Volet, "volet-acct-OLD");
        SeedEarning(db, "m1", 25m, Now.AddDays(-1));
        await db.SaveChangesAsync();

        await Create(db, GatewayMock(WalletType.Volet).Object).ExecutePayoutAsync("m1", Now);

        // Member changes their account afterwards.
        var wallet = await db.Wallets.SingleAsync(w => w.MemberId == "m1");
        wallet.AccountIdentifier = "volet-acct-NEW";
        await db.SaveChangesAsync();

        var attempt = await db.PayoutAttempts.SingleAsync();
        attempt.PayoutAccountSnapshot.Should().Be("volet-acct-OLD");
    }

    [Fact]
    public async Task PayoutAttempt_GroupsEarnings_WritesPayoutAttemptEarningRows()
    {
        using var db = TestDbContextFactory.Create();
        SeedWallet(db, "m1", WalletType.eWallet, "wallet@x.com");
        SeedEarning(db, "m1", 10m, Now.AddDays(-2));
        SeedEarning(db, "m1", 15m, Now.AddDays(-1));
        await db.SaveChangesAsync();

        await Create(db, GatewayMock().Object).ExecutePayoutAsync("m1", Now);

        var attempt = await db.PayoutAttempts.SingleAsync();
        attempt.EarningsCount.Should().Be(2);
        (await db.PayoutAttemptEarnings.CountAsync(x => x.PayoutAttemptId == attempt.Id)).Should().Be(2);
    }

    [Fact]
    public async Task Payout_OnlyEarningsDueOnOrBeforeProcessDate_AreIncluded()
    {
        using var db = TestDbContextFactory.Create();
        SeedWallet(db, "m1", WalletType.eWallet, "wallet@x.com");
        SeedEarning(db, "m1", 10m, Now.AddDays(-1)); // due
        SeedEarning(db, "m1", 99m, Now.AddDays(5));  // not yet due
        await db.SaveChangesAsync();

        var result = await Create(db, GatewayMock().Object).ExecutePayoutAsync("m1", Now);

        result.Value!.AmountUsd.Should().Be(10m);
        (await db.CommissionEarnings.CountAsync(e => e.Status == CommissionEarningStatus.Pending)).Should().Be(1);
    }

    [Fact]
    public async Task Payout_ReservedEarnings_ExcludedFromNewCandidates()
    {
        using var db = TestDbContextFactory.Create();
        SeedWallet(db, "m1", WalletType.eWallet, "wallet@x.com");
        var due = SeedEarning(db, "m1", 30m, Now.AddDays(-1));
        await db.SaveChangesAsync();

        // Simulate an open (non-failed) attempt already reserving the earning.
        var openAttempt = new PayoutAttempt
        {
            MemberId = "m1", WalletTypeSnapshot = WalletType.eWallet, PayoutAccountSnapshot = "wallet@x.com",
            AmountUsd = 30m, ProcessDateUtc = Now, Outcome = PayoutOutcome.Pending,
            AttemptedAtUtc = Now, EarningsCount = 1, DisbursementMode = DisbursementMode.CsvBulk,
            CreationDate = Now, CreatedBy = "seed"
        };
        db.PayoutAttempts.Add(openAttempt);
        await db.SaveChangesAsync();
        db.PayoutAttemptEarnings.Add(new PayoutAttemptEarning
        {
            PayoutAttemptId = openAttempt.Id, CommissionEarningId = due.Id, Amount = 30m,
            CreationDate = Now, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        var result = await Create(db, GatewayMock().Object).ExecutePayoutAsync("m1", Now);

        // Nothing new to pay — the only earning is reserved by the open attempt.
        result.Value!.EarningsCount.Should().Be(0);
    }
}
