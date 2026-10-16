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
using MLMConquerorGlobalEdition.Repository.Services.Funds;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Payout;

public class PayoutBatchExportServiceTests
{
    private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    private static PayoutBatchExportService Build(AppDbContext db, Mock<IReceiptStorage>? storage = null, IMemberFundsGate? gate = null)
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

        return new PayoutBatchExportService(db, resolver, st.Object, dt.Object, user.Object, gate ?? new TransactionOnlyGate(db));
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

    // ── Candado de fondos por miembro (Plan F) ────────────────────────────────

    /// <summary>Puerta de lote que niega el candado a quien se le diga y anota el orden de petición.</summary>
    private sealed class PuertaDeLote(AppDbContext db, params string[] ocupados) : IMemberFundsGate
    {
        public List<string> Pedidos { get; } = new();

        /// <summary>Lo que "pasa" justo antes de conceder el candado: otra operación que acaba de soltarlo.</summary>
        public Action<string>? AlConceder { get; init; }

        public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> EnterAsync(string memberId, CancellationToken ct = default) =>
            db.Database.BeginTransactionAsync(ct);

        public Task<bool> TryAcquireAsync(string memberId, TimeSpan timeout, CancellationToken ct = default)
        {
            Pedidos.Add(memberId);
            if (ocupados.Contains(memberId)) return Task.FromResult(false);
            AlConceder?.Invoke(memberId);
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task Export_PideLosCandadosEnOrdenOrdinalYDejaFueraAlMiembroOcupado()
    {
        using var db = TestDbContextFactory.Create();
        Seed(db, "AMB-3", WalletType.eWallet, 50m);
        Seed(db, "AMB-1", WalletType.eWallet, 40m);
        Seed(db, "AMB-2", WalletType.eWallet, 30m);
        var gate = new PuertaDeLote(db, "AMB-2");

        var result = await Build(db, gate: gate).ExportAsync(WalletType.eWallet, Now);

        gate.Pedidos.Should().Equal("AMB-1", "AMB-2", "AMB-3");
        result.Value!.MemberCount.Should().Be(2);
        (await db.PayoutAttempts.Select(a => a.MemberId).ToListAsync()).Should().BeEquivalentTo("AMB-1", "AMB-3");
        // El ocupado no queda reservado: entra en el próximo lote.
        (await Build(db).ExportAsync(WalletType.eWallet, Now)).Value!.MemberCount.Should().Be(1);
    }

    [Fact]
    public async Task Export_NoPagaSiUnDebitoDejoElTotalPorDebajoDelMinimo()
    {
        using var db = TestDbContextFactory.Create();
        Seed(db, "AMB-1", WalletType.eWallet, 50m);
        // Una compra de tokens con comisiones deja su débito entre la selección de candidatos y el
        // candado del lote: lo que se relee con el candado ya no llega al mínimo de 20.
        var gate = new PuertaDeLote(db)
        {
            AlConceder = m =>
            {
                db.CommissionEarnings.Add(new CommissionEarning
                {
                    BeneficiaryMemberId = m, Amount = -40m, Status = CommissionEarningStatus.Pending,
                    PaymentDate = Now.AddDays(-1), IsDeleted = false, CreationDate = Now, CreatedBy = "s"
                });
                db.SaveChanges();
            }
        };

        var result = await Build(db, gate: gate).ExportAsync(WalletType.eWallet, Now);

        result.Value!.MemberCount.Should().Be(0);
        db.PayoutAttemptEarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Export_UnDebitoPendienteSeDescuentaAunqueSuFechaSeaPosteriorAlProcessDate()
    {
        // El débito de una compra de tokens lleva PaymentDate = el momento de la compra; un lote con un
        // processDate anterior lo dejaba fuera y pagaba el crédito entero.
        using var db = TestDbContextFactory.Create();
        Seed(db, "AMB-1", WalletType.eWallet, 100m);
        db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = "AMB-1", Amount = -60m, Status = CommissionEarningStatus.Pending,
            PaymentDate = Now.AddDays(5), IsDeleted = false, CreationDate = Now, CreatedBy = "s"
        });
        db.SaveChanges();

        var result = await Build(db).ExportAsync(WalletType.eWallet, Now);

        result.Value!.TotalAmountUsd.Should().Be(40m);
        (await db.PayoutAttemptEarnings.CountAsync()).Should().Be(2);
    }
}
