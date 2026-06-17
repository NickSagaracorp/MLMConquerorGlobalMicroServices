using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Csv;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Payout;

public class PayoutBatchExportServiceTests
{
    private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    private static PayoutBatchExportService Build(AppDbContext db, Mock<IReceiptStorage>? storage = null)
    {
        var dt = new Mock<IDateTimeProvider>();
        dt.Setup(d => d.Now).Returns(Now);

        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns("admin-1");

        var st = storage ?? new Mock<IReceiptStorage>();
        st.Setup(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync("https://x/payout-receipts/batch.csv");

        var resolver = new PayoutCsvResolver(
            new IPayoutCsvFormatter[] { new EWalletPayoutCsvAdapter(), new VoletPayoutCsvAdapter() },
            new IPayoutResultCsvParser[] { new EWalletPayoutCsvAdapter(), new VoletPayoutCsvAdapter() });

        return new PayoutBatchExportService(db, resolver, st.Object, dt.Object, user.Object);
    }

    private static void Seed(AppDbContext db, string memberId, WalletType wt, decimal amount, decimal min = 20m)
    {
        db.Wallets.Add(new MemberProfilesWallet
        {
            MemberId = memberId, WalletType = wt, Status = WalletStatus.Approved,
            AccountIdentifier = memberId + "@x.com", IsPreferred = true, IsDeleted = false,
            CreationDate = Now, CreatedBy = "s", LastUpdateDate = Now
        });
        if (!db.PaymentGateways.Any(g => g.WalletType == wt))
            db.PaymentGateways.Add(new PaymentGatewayInfo
            {
                WalletType = wt, MinimumPayoutAmount = min, IsActive = true,
                DisplayName = wt.ToString(), Description = "d", Currency = "USD",
                AdminFee = 0m, AdminFeeKind = AdminFeeKind.Fixed,
                CreationDate = Now, CreatedBy = "s"
            });
        db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = memberId, Amount = amount,
            Status = CommissionEarningStatus.Pending, PaymentDate = Now.AddDays(-1),
            IsDeleted = false, CreationDate = Now, CreatedBy = "s"
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Export_CreatesBatch_ReservesEarnings_DoesNotMarkPaid()
    {
        using var db = TestDbContextFactory.Create();
        Seed(db, "AMB-1", WalletType.eWallet, 50m);

        var result = await Build(db).ExportAsync(WalletType.eWallet, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MemberCount.Should().Be(1);
        result.Value.TotalAmountUsd.Should().Be(50m);

        (await db.PayoutBatches.CountAsync()).Should().Be(1);
        var attempt = await db.PayoutAttempts.SingleAsync();
        attempt.DisbursementMode.Should().Be(DisbursementMode.CsvBulk);
        attempt.Outcome.Should().Be(PayoutOutcome.Pending);
        attempt.PayoutBatchId.Should().NotBeNull();
        (await db.PayoutAttemptEarnings.CountAsync()).Should().Be(1); // reserved
        (await db.CommissionEarnings.SingleAsync()).Status.Should().Be(CommissionEarningStatus.Pending); // NOT paid
    }

    [Fact]
    public async Task Export_ExcludesEarningsReservedByAnotherBatch()
    {
        using var db = TestDbContextFactory.Create();
        Seed(db, "AMB-1", WalletType.eWallet, 50m);

        await Build(db).ExportAsync(WalletType.eWallet, Now); // first export reserves

        var second = await Build(db).ExportAsync(WalletType.eWallet, Now);
        second.Value!.MemberCount.Should().Be(0); // nothing left to export
    }

    [Fact]
    public async Task Export_OnlyIncludesRequestedWalletType()
    {
        using var db = TestDbContextFactory.Create();
        Seed(db, "AMB-1", WalletType.eWallet, 50m);
        Seed(db, "AMB-2", WalletType.Volet, 60m);

        var result = await Build(db).ExportAsync(WalletType.Volet, Now);
        result.Value!.MemberCount.Should().Be(1);
        (await db.PayoutAttempts.SingleAsync(a => a.PayoutBatchId == result.Value.BatchId)).WalletTypeSnapshot
            .Should().Be(WalletType.Volet);
    }

    [Fact]
    public async Task Export_UnsupportedGateway_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var result = await Build(db).ExportAsync(WalletType.Crypto, Now);
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PAYOUT_CSV_GATEWAY_NOT_SUPPORTED");
    }
}
