using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Jobs;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Anchoring;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Payout;

public class ReceiptAnchorJobTests
{
    private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    private static ReceiptAnchorJob Job(AppDbContext db, IDocumentAnchorService? anchor = null)
    {
        var dt = new Mock<IDateTimeProvider>(); dt.Setup(d => d.Now).Returns(Now);
        return new ReceiptAnchorJob(db, anchor ?? new StubDocumentAnchorService(), dt.Object,
            NullLogger<ReceiptAnchorJob>.Instance);
    }

    private static PayoutAttempt SeedIssued(AppDbContext db, string sha, DateTime completed)
    {
        var a = new PayoutAttempt
        {
            MemberId = "AMB-1", WalletTypeSnapshot = WalletType.eWallet, PayoutAccountSnapshot = "x",
            AmountUsd = 10m, ProcessDateUtc = Now, Outcome = PayoutOutcome.Success,
            AttemptedAtUtc = completed, CompletedAtUtc = completed, EarningsCount = 1,
            ReceiptUrl = "u", ReceiptSha256 = sha, CreationDate = Now, CreatedBy = "seed"
        };
        db.PayoutAttempts.Add(a);
        db.SaveChanges();
        return a;
    }

    [Fact]
    public async Task Chains_SequenceAndPrevHash_InOrder_AndAnchors()
    {
        using var db = TestDbContextFactory.Create();
        var a1 = SeedIssued(db, "sha-1", Now.AddMinutes(1));
        var a2 = SeedIssued(db, "sha-2", Now.AddMinutes(2));

        await Job(db).ExecuteAsync();

        a1.ReceiptLedgerSeq.Should().Be(1);
        a2.ReceiptLedgerSeq.Should().Be(2);
        a1.ReceiptPrevHash.Should().Be(MerkleTree.ChainHash(MerkleTree.Genesis, "sha-1"));
        a2.ReceiptPrevHash.Should().Be(MerkleTree.ChainHash(a1.ReceiptPrevHash!, "sha-2"));
        a1.ReceiptAnchorRef.Should().NotBeNullOrEmpty();
        a2.ReceiptAnchorRef.Should().Be(a1.ReceiptAnchorRef); // same batch root
    }

    [Fact]
    public async Task Idempotent_SecondRun_DoesNotRechainOrChangeSeq()
    {
        using var db = TestDbContextFactory.Create();
        var a1 = SeedIssued(db, "sha-1", Now.AddMinutes(1));
        await Job(db).ExecuteAsync();
        var seqAfterFirst = a1.ReceiptLedgerSeq;

        await Job(db).ExecuteAsync(); // nothing new

        a1.ReceiptLedgerSeq.Should().Be(seqAfterFirst);
    }

    [Fact]
    public async Task SecondBatch_ContinuesChainFromHead()
    {
        using var db = TestDbContextFactory.Create();
        var a1 = SeedIssued(db, "sha-1", Now.AddMinutes(1));
        await Job(db).ExecuteAsync();
        var a2 = SeedIssued(db, "sha-2", Now.AddMinutes(5));

        await Job(db).ExecuteAsync();

        a2.ReceiptLedgerSeq.Should().Be(2);
        a2.ReceiptPrevHash.Should().Be(MerkleTree.ChainHash(a1.ReceiptPrevHash!, "sha-2"));
    }
}
