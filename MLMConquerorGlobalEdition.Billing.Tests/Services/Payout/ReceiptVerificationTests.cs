using FluentAssertions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Jobs;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Anchoring;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using System.Security.Cryptography;
using Xunit;
using IDateTimeProvider = MLMConquerorGlobalEdition.Billing.Services.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Payout;

public class ReceiptVerificationTests
{
    private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    private static string Hash(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    [Fact]
    public async Task Verify_GoodReceipt_HashMatchesAndChainValid()
    {
        using var db = TestDbContextFactory.Create();
        var pdf = new byte[] { 9, 8, 7, 6 };
        var a = new PayoutAttempt
        {
            MemberId = "AMB-1", WalletTypeSnapshot = WalletType.eWallet, PayoutAccountSnapshot = "x",
            AmountUsd = 10m, ProcessDateUtc = Now, Outcome = PayoutOutcome.Success,
            AttemptedAtUtc = Now, CompletedAtUtc = Now, EarningsCount = 1,
            ReceiptUrl = "u", ReceiptSha256 = Hash(pdf), CreationDate = Now, CreatedBy = "seed"
        };
        db.PayoutAttempts.Add(a);
        db.SaveChanges();
        // chain it (seq 1)
        await new ReceiptAnchorJob(db, new StubDocumentAnchorService(),
            Mock.Of<IDateTimeProvider>(d => d.Now == Now),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ReceiptAnchorJob>.Instance).ExecuteAsync();

        var storage = new Mock<IReceiptStorage>();
        storage.Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(pdf);

        var result = await new ReceiptVerificationService(db, storage.Object).VerifyAsync(a);

        result.HasReceipt.Should().BeTrue();
        result.HashMatches.Should().BeTrue();
        result.ChainValid.Should().BeTrue();
        result.Anchored.Should().BeTrue();
    }

    [Fact]
    public async Task Verify_TamperedReceipt_FailsHash()
    {
        using var db = TestDbContextFactory.Create();
        var original = new byte[] { 1, 2, 3 };
        var a = new PayoutAttempt
        {
            MemberId = "AMB-1", WalletTypeSnapshot = WalletType.eWallet, PayoutAccountSnapshot = "x",
            AmountUsd = 10m, ProcessDateUtc = Now, Outcome = PayoutOutcome.Success,
            AttemptedAtUtc = Now, CompletedAtUtc = Now, EarningsCount = 1,
            ReceiptUrl = "u", ReceiptSha256 = Hash(original), CreationDate = Now, CreatedBy = "seed"
        };
        db.PayoutAttempts.Add(a);
        db.SaveChanges();

        var storage = new Mock<IReceiptStorage>();
        storage.Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new byte[] { 9, 9, 9 }); // tampered content

        var result = await new ReceiptVerificationService(db, storage.Object).VerifyAsync(a);

        result.HashMatches.Should().BeFalse();
        result.Detail.Should().Contain("TAMPERED");
    }

    [Fact]
    public async Task Verify_NoReceipt_ReportsHasReceiptFalse()
    {
        using var db = TestDbContextFactory.Create();
        var a = new PayoutAttempt
        {
            MemberId = "AMB-1", WalletTypeSnapshot = WalletType.eWallet, PayoutAccountSnapshot = "x",
            AmountUsd = 10m, ProcessDateUtc = Now, Outcome = PayoutOutcome.Success,
            AttemptedAtUtc = Now, CompletedAtUtc = Now, EarningsCount = 1,
            CreationDate = Now, CreatedBy = "seed"
        };
        db.PayoutAttempts.Add(a);
        db.SaveChanges();

        var result = await new ReceiptVerificationService(db, new Mock<IReceiptStorage>().Object).VerifyAsync(a);

        result.HasReceipt.Should().BeFalse();
    }
}
