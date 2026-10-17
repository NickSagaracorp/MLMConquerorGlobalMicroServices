using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// La cuota con eWallet (Plan K, K11), en dos fases como la compra de tokens: se reserva el ciclo, se
/// relee el saldo real con el candado, se debita y se verifica. Lo dudoso queda a conciliar y no se
/// reintenta; un rechazo es un intento fallido.
/// </summary>
public class CobroDeCuotaConEWalletTests
{
    private const string Miembro = "AMB-1";
    private const string Usuario = "amb-1";

    private static async Task<AppDbContext> ConMiembroAsync(bool conSaldo = true)
    {
        var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, Miembro, conSaldo: conSaldo);
        await CuotaAnualDb.EstadoAsync(db, Miembro);
        return db;
    }

    private static Mock<IEWalletFunds> EWallet(decimal saldo, EWalletDebitOutcome? resultado = null, decimal? saldoAlDebitar = null)
    {
        var m = new Mock<IEWalletFunds>();
        m.Setup(e => e.UserNameOfAsync(Miembro, It.IsAny<CancellationToken>())).ReturnsAsync(Usuario);
        m.SetupSequence(e => e.BalanceAsync(Usuario, It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<decimal>.Success(saldo))
         .ReturnsAsync(Result<decimal>.Success(saldoAlDebitar ?? saldo));
        m.Setup(e => e.DebitAsync(Usuario, 99m, It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(resultado ?? new EWalletDebitOutcome(EWalletDebitStatus.Completed, null, "ok"));
        return m;
    }

    private static Task<Result<AnnualFeeChargeOutcome>> Cobrar(AppDbContext db, Mock<IEWalletFunds> ew, TransactionOnlyGate? candado = null) =>
        CobroAutomaticoDeCuotaAnualTests.Servicio(db, candado: candado ?? new TransactionOnlyGate(db), ewallet: ew)
            .ChargeDueAsync("anual-" + Miembro, force: false);

    [Fact]
    public async Task Sin_token_ni_comisiones_paga_con_la_eWallet_en_dos_fases()
    {
        await using var db = await ConMiembroAsync();
        var ew = EWallet(150m);
        var candado = new TransactionOnlyGate(db);

        var r = await Cobrar(db, ew, candado);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Success);
        r.Value.FundingSource.Should().Be(AnnualFeeMethods.EWallet);
        candado.Entered.Count(m => m == Miembro).Should().BeGreaterThanOrEqualTo(2);

        var pago = await db.AnnualFeePayments.SingleAsync();
        pago.Status.Should().Be(AnnualFeePaymentStatus.Completed);
        pago.Method.Should().Be(AnnualFeePaymentMethod.EWallet);
        ew.Verify(e => e.DebitAsync(Usuario, 99m, 150m, pago.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        var ph = await db.PaymentHistories.SingleAsync();
        ph.GatewayName.Should().Be("eWallet");
        ph.Amount.Should().Be(99m);
        pago.PaymentHistoryId.Should().Be(ph.Id);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Paid);
        (await db.SubscriptionBillingStates.AsNoTracking().SingleAsync()).NextBillingDate.Should().Be(new DateTime(2028, 6, 10)); // K4 revisado
        (await db.RecurringBillingAttempts.SingleAsync()).FundingSource.Should().Be(RecurringFundingSource.EWallet);
    }

    [Fact]
    public async Task Con_la_preferencia_apagada_no_se_llama_a_iPayout()
    {
        await using var db = await ConMiembroAsync(conSaldo: false);
        var ew = EWallet(150m);

        var r = await Cobrar(db, ew);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Failed);
        ew.Verify(e => e.UserNameOfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_rechazo_es_un_intento_fallido_y_libera_el_ciclo()
    {
        await using var db = await ConMiembroAsync();
        var ew = EWallet(150m, new EWalletDebitOutcome(EWalletDebitStatus.Rejected, "E12", "rechazado"));

        var r = await Cobrar(db, ew);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Failed);
        var pago = await db.AnnualFeePayments.SingleAsync();
        pago.Status.Should().Be(AnnualFeePaymentStatus.Failed);
        pago.FailureCode.Should().Be(AnnualFeeErrors.EWalletRejected);
        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatus.Cancelled);
        (await db.SubscriptionBillingStates.AsNoTracking().SingleAsync()).CurrentAttemptIndex.Should().Be(1);
    }

    [Fact]
    public async Task Un_resultado_dudoso_queda_a_conciliar_y_no_se_reintenta()
    {
        await using var db = await ConMiembroAsync();
        var ew = EWallet(150m, new EWalletDebitOutcome(EWalletDebitStatus.NeedsReconciliation, "EWALLET_DEBIT_PENDING", "?"));

        var r = await Cobrar(db, ew);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Skipped);
        r.Value.FailureReason.Should().Be(AnnualFeeErrors.NeedsReview);
        (await db.AnnualFeePayments.SingleAsync()).Status.Should().Be(AnnualFeePaymentStatus.NeedsReconciliation);
        var estado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        estado.NextBillingDate.Should().Be(new DateTime(2027, 6, 5));
        estado.NextAttemptDate.Should().Be(CuotaAnualDb.Ahora.Date.AddDays(1));
        estado.LastAttemptOutcome.Should().Be("NeedsReconciliation");

        // La pasada siguiente ve el pago a conciliar y no vuelve a debitar.
        var otra = await Cobrar(db, EWallet(150m));
        otra.Value!.FailureReason.Should().Be(AnnualFeeErrors.NeedsReview);
        ew.Verify(e => e.DebitAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Si_el_debito_lanza_queda_a_conciliar()
    {
        await using var db = await ConMiembroAsync();
        var ew = EWallet(150m);
        ew.Setup(e => e.DebitAsync(Usuario, 99m, It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ThrowsAsync(new TimeoutException("i-Payout no contestó"));

        await Cobrar(db, ew);

        (await db.AnnualFeePayments.SingleAsync()).Status.Should().Be(AnnualFeePaymentStatus.NeedsReconciliation);
    }

    [Fact]
    public async Task Si_el_saldo_ya_no_alcanza_en_la_segunda_lectura_no_se_debita()
    {
        await using var db = await ConMiembroAsync();
        var ew = EWallet(150m, saldoAlDebitar: 50m);

        var r = await Cobrar(db, ew);

        r.Value!.Outcome.Should().Be(AnnualFeeOutcomes.Failed);
        var pago = await db.AnnualFeePayments.SingleAsync();
        pago.Status.Should().Be(AnnualFeePaymentStatus.Failed);
        pago.FailureCode.Should().Be(AnnualFeeErrors.InsufficientEWalletBalance);
        ew.Verify(e => e.DebitAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
