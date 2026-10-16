using FluentAssertions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Constants;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Con qué saldo de comisiones se pueden pagar tokens: solo lo ya pagable (PaymentDate &lt;= Now)
/// y no metido en un pago en curso.
/// </summary>
public class SaldoParaComprarTokensTests
{
    private const string Yo = "AMB-1";
    private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0);

    private static Mock<IDateTimeProvider> Reloj()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(Now);
        m.Setup(d => d.UtcNow).Returns(Now);
        return m;
    }

    private static CommissionEarning Comision(decimal monto, DateTime pago) => new()
    {
        BeneficiaryMemberId = Yo, CommissionTypeId = 1, Amount = monto,
        Status = CommissionEarningStatus.Pending, EarnedDate = pago.AddDays(-7), PaymentDate = pago,
        CreatedBy = "seed", CreationDate = pago.AddDays(-7), LastUpdateDate = pago.AddDays(-7)
    };

    [Fact]
    public async Task Gastable_excluye_lo_que_vence_despues_y_lo_reservado_por_un_pago()
    {
        var db = TestDbContextFactory.Create();
        var reservada = Comision(200m, Now.AddDays(-2));
        db.CommissionEarnings.AddRange(Comision(500m, Now.AddDays(-1)), Comision(1000m, Now.AddDays(20)), reservada);
        var intento = new PayoutAttempt
        {
            MemberId = Yo, WalletTypeSnapshot = WalletType.eWallet, PayoutAccountSnapshot = "x",
            AmountUsd = 200m, ProcessDateUtc = Now, Outcome = PayoutOutcome.Pending,
            AttemptedAtUtc = Now, EarningsCount = 1, CreatedBy = "seed", CreationDate = Now
        };
        db.PayoutAttempts.Add(intento);
        await db.SaveChangesAsync();
        db.PayoutAttemptEarnings.Add(new PayoutAttemptEarning
        {
            PayoutAttemptId = intento.Id, CommissionEarningId = reservada.Id, Amount = 200m,
            CreatedBy = "seed", CreationDate = Now
        });
        await db.SaveChangesAsync();

        (await new CommissionBalanceService(db, Reloj().Object).GetSpendableAsync(Yo)).Should().Be(500m);
    }

    [Fact]
    public async Task Pagar_una_compra_deja_un_debito_negativo_y_el_pago()
    {
        var db = TestDbContextFactory.Create();
        db.CommissionEarnings.Add(Comision(500m, Now.AddDays(-1)));
        await db.SaveChangesAsync();
        var sut = new CommissionBalanceService(db, Reloj().Object);

        var r = await sut.FundTokenPurchaseAsync(Yo, 199.98m, "ord-1", "user-1");

        r.IsSuccess.Should().BeTrue();
        var debito = db.CommissionEarnings.Single(e => e.Amount < 0);
        debito.Amount.Should().Be(-199.98m);
        debito.SourceOrderId.Should().Be("ord-1");
        debito.PaymentDate.Should().Be(Now);
        var pago = db.PaymentHistories.Single();
        pago.Id.Should().Be(r.Value);
        pago.GatewayName.Should().Be("CommissionBalance");
        pago.GatewayTransactionId.Should().Be(debito.Id);
        (await sut.GetSpendableAsync(Yo)).Should().Be(300.02m);
    }

    [Fact]
    public async Task Sin_saldo_no_escribe_nada()
    {
        var db = TestDbContextFactory.Create();
        db.CommissionEarnings.Add(Comision(50m, Now.AddDays(-1)));
        await db.SaveChangesAsync();

        var r = await new CommissionBalanceService(db, Reloj().Object).FundTokenPurchaseAsync(Yo, 99.99m, "ord-1", "user-1");

        r.ErrorCode.Should().Be(TokenPurchaseErrors.InsufficientCommissionBalance);
        db.CommissionEarnings.Should().HaveCount(1);
        db.PaymentHistories.Should().BeEmpty();
    }
}
