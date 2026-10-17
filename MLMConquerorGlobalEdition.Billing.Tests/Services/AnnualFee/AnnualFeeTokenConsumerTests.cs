using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// Qué token paga la cuota (Plan K, K8): una instancia con código, del miembro, viva y sin caducar, de
/// los tipos 23 ("Annual: Biz Center") y 10 ("Annual Fee"), el 23 antes y el más antiguo primero.
/// Gastarla la marca Used ligada al pedido, deja el asiento del ledger y baja el saldo agregado.
/// </summary>
public class AnnualFeeTokenConsumerTests
{
    private const string Miembro = "AMB-1";
    private static readonly DateTime Ahora = CuotaAnualDb.Ahora;

    [Fact]
    public async Task Prefiere_el_tipo_23_aunque_el_10_sea_mas_antiguo()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.TokenAsync(db, Miembro, AnnualFeeRules.AnnualFeeTokenTypeId, "DIEZ", creado: Ahora.AddDays(-30));
        await CuotaAnualDb.TokenAsync(db, Miembro, AnnualFeeRules.AnnualBizCenterTokenTypeId, "VEINTITRES", creado: Ahora.AddDays(-1));

        (await new AnnualFeeTokenConsumer(db).FindAsync(Miembro, Ahora))!.ReferenceId.Should().Be("VEINTITRES");
    }

    [Fact]
    public async Task Dentro_del_mismo_tipo_el_mas_antiguo()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.TokenAsync(db, Miembro, codigo: "NUEVO", creado: Ahora.AddDays(-1));
        await CuotaAnualDb.TokenAsync(db, Miembro, codigo: "VIEJO", creado: Ahora.AddDays(-20));

        (await new AnnualFeeTokenConsumer(db).FindAsync(Miembro, Ahora))!.ReferenceId.Should().Be("VIEJO");
    }

    [Fact]
    public async Task No_vale_un_token_usado_anulado_caducado_de_otro_tipo_o_de_otro_miembro()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.TokenAsync(db, Miembro, codigo: "USADO", estado: TokenInstanceStatus.Used);
        await CuotaAnualDb.TokenAsync(db, Miembro, codigo: "ANULADO", estado: TokenInstanceStatus.Voided);
        await CuotaAnualDb.TokenAsync(db, Miembro, codigo: "CADUCADO", caduca: Ahora.AddDays(-1));
        await CuotaAnualDb.TokenAsync(db, Miembro, tipo: 24, codigo: "LEGADO");
        await CuotaAnualDb.TokenAsync(db, Miembro, tipo: 103, codigo: "PLAN");
        await CuotaAnualDb.TokenAsync(db, "AMB-2", codigo: "AJENO");

        (await new AnnualFeeTokenConsumer(db).FindAsync(Miembro, Ahora)).Should().BeNull();
        (await AnnualFeeTokenQueries.Available(db, Miembro, Ahora).CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_token_distribuido_al_miembro_tambien_paga_su_cuota()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.TokenAsync(db, Miembro, codigo: "REGALADO", estado: TokenInstanceStatus.Distributed);

        (await new AnnualFeeTokenConsumer(db).FindAsync(Miembro, Ahora))!.ReferenceId.Should().Be("REGALADO");
    }

    [Fact]
    public async Task Gastarlo_lo_marca_usado_deja_el_ledger_y_baja_el_saldo()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.TokenAsync(db, Miembro, codigo: "PAGA");
        var consumidor = new AnnualFeeTokenConsumer(db);

        var instancia = (await consumidor.FindAsync(Miembro, Ahora))!;
        var gastado = await consumidor.ConsumeAsync(instancia, "ord-1", "prueba", Ahora);
        await db.SaveChangesAsync();

        gastado.Code.Should().Be("PAGA");
        gastado.TokenTransactionId.Should().Be(instancia.Id);
        gastado.TokenTypeId.Should().Be(AnnualFeeRules.AnnualBizCenterTokenTypeId);

        var fila = await db.TokenTransactions.AsNoTracking().SingleAsync(t => t.ReferenceId == "PAGA");
        fila.Status.Should().Be(TokenInstanceStatus.Used);
        fila.UsedOnOrderId.Should().Be("ord-1");
        fila.UsedByMemberId.Should().Be(Miembro);
        fila.UsedAt.Should().Be(Ahora);

        (await db.TokenTransactions.CountAsync(t => t.ReferenceId == null && t.TransactionType == TokenTransactionType.Used))
            .Should().Be(1);
        (await db.TokenBalances.SingleAsync()).Balance.Should().Be(0);
    }
}
