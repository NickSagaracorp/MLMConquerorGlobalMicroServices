using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using Xunit;
using IDateTimeProvider = MLMConquerorGlobalEdition.Billing.Services.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Payout;

// ─── Task 1: Renderer / Storage / Naming ─────────────────────────────────────

public class ReceiptRendererStorageTests
{
    private static PayoutReceiptData SampleData() => new(
        PayoutAttemptId: 42, MemberId: "AMB-1", FullName: "Ana Diaz",
        WalletType: WalletType.eWallet, PayoutAccountSnapshot: "ana@x.com",
        AmountUsd: 50m, ProcessDateUtc: new DateTime(2026, 6, 10),
        CompletedAtUtc: new DateTime(2026, 6, 10, 12, 0, 0), GatewayTransactionId: "txn-1",
        Earnings: new List<ReceiptEarningLine> { new("CE-1", 30m), new("CE-2", 20m) });

    [Fact]
    public void Renderer_ProducesNonEmptyPdfBytes()
    {
        var bytes = new ITextReceiptPdfRenderer().Render(SampleData());
        bytes.Should().NotBeNullOrEmpty();
        // PDF magic header "%PDF"
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public void FileNaming_IsDeterministic()
    {
        var a = PayoutReceiptFileNaming.Build(42, "AMB-1");
        var b = PayoutReceiptFileNaming.Build(42, "AMB-1");
        a.Should().Be(b);
        a.Should().EndWith("payout-42.pdf");
    }

    [Fact]
    public async Task LocalStorage_SaveThenRead_RoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "receipt-tests-" + Guid.NewGuid().ToString("N"));
        var storage = new LocalReceiptStorage(dir, "https://localhost:7001");
        var url = await storage.SaveAsync("f.pdf", new byte[] { 1, 2, 3 });
        url.Should().Be("https://localhost:7001/payout-receipts/f.pdf");
        (await storage.ReadAsync("f.pdf")).Should().Equal(new byte[] { 1, 2, 3 });
        (await storage.ReadAsync("missing.pdf")).Should().BeNull();
        Directory.Delete(dir, true);
    }
}

// ─── Task 2: Receipt Service ──────────────────────────────────────────────────

public class PayoutReceiptServiceTests
{
    private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    private static (PayoutReceiptService svc, Mock<IEmailService> email, AppDbContext db) Build(
        bool? autoSendToggle = null)
    {
        var db = TestDbContextFactory.Create();
        db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = "AMB-1", Email = "ana@x.com", FirstName = "Ana", LastName = "Diaz",
            DefaultLanguage = "en", CreationDate = Now, CreatedBy = "seed", LastUpdateDate = Now
        });
        if (autoSendToggle.HasValue)
            db.GlobalParameters.Add(new GlobalParameter
            {
                Key = PayoutReceiptService.AutoSendKey, Value = autoSendToggle.Value.ToString(),
                CreationDate = Now, CreatedBy = "seed"
            });
        db.SaveChanges();

        var renderer = new Mock<IReceiptPdfRenderer>();
        renderer.Setup(r => r.Render(It.IsAny<PayoutReceiptData>())).Returns(new byte[] { 1, 2, 3, 4 });
        var storage = new Mock<IReceiptStorage>();
        storage.Setup(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync("https://x/payout-receipts/f.pdf");
        var email = new Mock<IEmailService>();
        var dt = new Mock<IDateTimeProvider>(); dt.Setup(d => d.Now).Returns(Now);

        var svc = new PayoutReceiptService(db, renderer.Object, storage.Object, email.Object, dt.Object,
            NullLogger<PayoutReceiptService>.Instance);
        return (svc, email, db);
    }

    private static PayoutAttempt SeedAttempt(AppDbContext db)
    {
        var a = new PayoutAttempt
        {
            MemberId = "AMB-1", WalletTypeSnapshot = WalletType.eWallet, PayoutAccountSnapshot = "ana@x.com",
            AmountUsd = 50m, ProcessDateUtc = Now, Outcome = PayoutOutcome.Success,
            AttemptedAtUtc = Now, CompletedAtUtc = Now, EarningsCount = 1, GatewayTransactionId = "txn-1",
            CreationDate = Now, CreatedBy = "seed"
        };
        db.PayoutAttempts.Add(a);
        db.SaveChanges();
        db.PayoutAttemptEarnings.Add(new PayoutAttemptEarning
        {
            PayoutAttemptId = a.Id, CommissionEarningId = "CE-1", Amount = 50m, CreationDate = Now, CreatedBy = "seed"
        });
        db.SaveChanges();
        return a;
    }

    [Fact]
    public async Task IssueReceipt_OnSuccess_StoresUrlAndHash()
    {
        var (svc, _, db) = Build(autoSendToggle: false);
        var a = SeedAttempt(db);

        await svc.IssueReceiptAsync(a);

        a.ReceiptUrl.Should().NotBeNullOrEmpty();
        a.ReceiptSha256.Should().NotBeNullOrEmpty();
        a.ReceiptSha256!.Length.Should().Be(64); // sha256 hex
    }

    [Fact]
    public async Task EmailAutoSend_WhenToggleOff_DoesNotSend()
    {
        var (svc, email, db) = Build(autoSendToggle: false);
        var a = SeedAttempt(db);

        await svc.IssueReceiptAsync(a);

        email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EmailAutoSend_WhenToggleOnOrDefault_Sends()
    {
        var (svc, email, db) = Build(autoSendToggle: null); // no param → default ON
        var a = SeedAttempt(db);

        await svc.IssueReceiptAsync(a);

        email.Verify(e => e.SendAsync("ana@x.com", "Ana Diaz", "en",
            NotificationEvents.PayoutReceiptIssued, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IssueReceipt_WhenRendererThrows_DoesNotThrow()
    {
        var db = TestDbContextFactory.Create();
        db.MemberProfiles.Add(new MemberProfile { MemberId = "AMB-1", Email = "a@x.com", FirstName = "A", LastName = "B", DefaultLanguage = "en", CreationDate = Now, CreatedBy = "s", LastUpdateDate = Now });
        db.SaveChanges();
        var a = SeedAttempt(db);

        var renderer = new Mock<IReceiptPdfRenderer>();
        renderer.Setup(r => r.Render(It.IsAny<PayoutReceiptData>())).Throws(new Exception("render boom"));
        var svc = new PayoutReceiptService(db, renderer.Object, new Mock<IReceiptStorage>().Object,
            new Mock<IEmailService>().Object, Mock.Of<IDateTimeProvider>(d => d.Now == Now),
            NullLogger<PayoutReceiptService>.Instance);

        var act = async () => await svc.IssueReceiptAsync(a);
        await act.Should().NotThrowAsync(); // best-effort
        a.ReceiptUrl.Should().BeNull();
    }

    [Fact]
    public async Task Resend_SendsEmailRegardlessOfToggle()
    {
        var (svc, email, db) = Build(autoSendToggle: false); // toggle OFF
        var a = SeedAttempt(db);

        var sent = await svc.ResendReceiptAsync(a);

        sent.Should().BeTrue();
        email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            NotificationEvents.PayoutReceiptIssued, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
