using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// La cuota pagada con comisiones (Plan K, K9): un débito negativo ligado al pedido y su PaymentHistory,
/// SIN emitir tokens (FundWithCommissionBalanceAsync deja uno de regalo, R8). Comparte núcleo con la
/// compra de tokens, que sigue igual. Quien llama tiene el candado de fondos.
/// </summary>
public class DebitoDeComisionesDeLaCuotaTests
{
    private const string Miembro = "AMB-1";

    [Fact]
    public async Task Debita_la_cuota_y_deja_su_pago_sin_emitir_tokens()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        var r = await new CommissionBalanceService(db, CuotaAnualDb.Reloj()).FundAnnualFeeAsync(Miembro, 99m, "ord-1", "prueba");

        r.IsSuccess.Should().BeTrue();
        var debito = await db.CommissionEarnings.SingleAsync(e => e.Amount < 0);
        debito.Amount.Should().Be(-99m);
        debito.SourceOrderId.Should().Be("ord-1");
        debito.Notes.Should().StartWith("Annual fee debit");
        r.Value!.DebitEarningId.Should().Be(debito.Id);

        var pago = await db.PaymentHistories.SingleAsync();
        pago.Id.Should().Be(r.Value.PaymentHistoryId);
        pago.GatewayName.Should().Be("CommissionBalance");
        pago.GatewayTransactionId.Should().Be(debito.Id);
        pago.Amount.Should().Be(99m);

        (await db.TokenTransactions.CountAsync()).Should().Be(0);
        (await db.TokenBalances.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_saldo_gastable_no_escribe_nada()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m, pagable: CuotaAnualDb.Ahora.AddDays(3));

        var r = await new CommissionBalanceService(db, CuotaAnualDb.Reloj()).FundAnnualFeeAsync(Miembro, 99m, "ord-1", "prueba");

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(AnnualFeeErrors.InsufficientCommissionBalance);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(0);
        (await db.PaymentHistories.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task La_compra_de_tokens_sigue_debitando_con_su_nota()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.ComisionAsync(db, Miembro, 150m);

        var r = await new CommissionBalanceService(db, CuotaAnualDb.Reloj()).FundTokenPurchaseAsync(Miembro, 50m, "ord-t", "prueba");

        r.IsSuccess.Should().BeTrue();
        (await db.CommissionEarnings.SingleAsync(e => e.Amount < 0)).Notes.Should().StartWith("Token purchase debit");
        (await db.PaymentHistories.SingleAsync()).Id.Should().Be(r.Value);
    }
}
