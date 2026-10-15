using FluentAssertions;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Tests;

/// <summary>
/// La máquina de estados de una compra de tokens: se completa una sola vez, lo dudoso queda a
/// conciliar sin emitir, y todo lo que está en vuelo ocupa cupo del mes.
/// </summary>
public class TokenPurchaseTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0);

    [Theory]
    [InlineData(TokenPurchaseStatus.Pending)]
    [InlineData(TokenPurchaseStatus.AwaitingCryptoConfirmation)]
    [InlineData(TokenPurchaseStatus.NeedsReconciliation)]
    public void Lo_que_esta_en_vuelo_se_completa_y_guarda_el_pago(TokenPurchaseStatus desde)
    {
        var p = new TokenPurchase { Status = desde };

        p.Complete("ph-1", Now, "user-1");

        p.Status.Should().Be(TokenPurchaseStatus.Completed);
        p.PaymentHistoryId.Should().Be("ph-1");
        p.CompletedAt.Should().Be(Now);
        p.LastUpdateBy.Should().Be("user-1");
    }

    [Theory]
    [InlineData(TokenPurchaseStatus.Completed)]
    [InlineData(TokenPurchaseStatus.Failed)]
    public void Una_compra_cerrada_no_se_completa_otra_vez(TokenPurchaseStatus status)
    {
        var act = () => new TokenPurchase { Status = status }.Complete("ph-3", Now, "user-1");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Fallan_las_pendientes_y_las_que_estaban_a_conciliar_pero_no_las_de_cripto()
    {
        var pendiente = new TokenPurchase { Status = TokenPurchaseStatus.Pending };
        pendiente.Fail("CARD_CHARGE_FAILED", Now, "user-1");
        pendiente.Status.Should().Be(TokenPurchaseStatus.Failed);
        pendiente.FailureCode.Should().Be("CARD_CHARGE_FAILED");

        var aConciliar = new TokenPurchase { Status = TokenPurchaseStatus.NeedsReconciliation };
        aConciliar.Fail("NOT_CHARGED", Now, "tesoreria@x.com");
        aConciliar.Status.Should().Be(TokenPurchaseStatus.Failed);

        var cripto = new TokenPurchase { Status = TokenPurchaseStatus.AwaitingCryptoConfirmation };
        var act = () => cripto.Fail("X", Now, "user-1");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Solo_una_pendiente_pasa_a_conciliar()
    {
        var p = new TokenPurchase { Status = TokenPurchaseStatus.Pending };

        p.MarkForReconciliation("EWALLET_DEBIT_PENDING", "saldo sin cambios", Now, "user-1");

        p.Status.Should().Be(TokenPurchaseStatus.NeedsReconciliation);
        p.FailureCode.Should().Be("EWALLET_DEBIT_PENDING");
        p.Notes.Should().Be("saldo sin cambios");

        var act = () => new TokenPurchase { Status = TokenPurchaseStatus.Completed }
            .MarkForReconciliation("X", null, Now, "user-1");
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(TokenPurchaseStatus.Pending, true)]
    [InlineData(TokenPurchaseStatus.AwaitingCryptoConfirmation, true)]
    [InlineData(TokenPurchaseStatus.NeedsReconciliation, true)]
    [InlineData(TokenPurchaseStatus.Completed, false)]
    [InlineData(TokenPurchaseStatus.Failed, false)]
    public void Solo_las_compras_en_vuelo_reservan_cupo(TokenPurchaseStatus status, bool reserva) =>
        new TokenPurchase { Status = status }.IsReservation.Should().Be(reserva);

    [Theory]
    [InlineData(TokenPurchaseStatus.Cancelled)]
    [InlineData(TokenPurchaseStatus.Rejected)]
    [InlineData(TokenPurchaseStatus.Expired)]
    public void Las_salidas_de_cripto_no_reservan_cupo_ni_se_completan(TokenPurchaseStatus status)
    {
        var p = new TokenPurchase { Status = status };

        p.IsReservation.Should().BeFalse();
        var act = () => p.Complete("ph-1", Now, "admin-1");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void El_miembro_cancela_su_cripto_sin_pagar()
    {
        var p = new TokenPurchase { Status = TokenPurchaseStatus.AwaitingCryptoConfirmation };

        p.CancelByMember(Now, "user-1");

        p.Status.Should().Be(TokenPurchaseStatus.Cancelled);
        p.FailureCode.Should().Be(TokenPurchase.CancelledByMemberCode);
        p.LastUpdateBy.Should().Be("user-1");
        p.LastUpdateDate.Should().Be(Now);
    }

    [Fact]
    public void El_admin_rechaza_con_motivo()
    {
        var p = new TokenPurchase { Status = TokenPurchaseStatus.AwaitingCryptoConfirmation };

        p.Reject("  No llegó la transferencia  ", Now, "admin@x.com");

        p.Status.Should().Be(TokenPurchaseStatus.Rejected);
        p.FailureCode.Should().Be(TokenPurchase.RejectedByAdminCode);
        p.Notes.Should().Be("No llegó la transferencia");
    }

    [Fact]
    public void Caduca_a_los_dias_configurados_y_no_antes()
    {
        var p = new TokenPurchase { Status = TokenPurchaseStatus.AwaitingCryptoConfirmation, CreationDate = Now };

        p.IsCryptoExpired(Now.AddDays(3).AddSeconds(-1), 3).Should().BeFalse();
        p.IsCryptoExpired(Now.AddDays(3), 3).Should().BeTrue();
        p.IsCryptoExpired(Now.AddYears(1), 0).Should().BeFalse("0 días apaga la caducidad");

        p.Expire(Now.AddDays(3), "token-purchase-expiry");

        p.Status.Should().Be(TokenPurchaseStatus.Expired);
        p.FailureCode.Should().Be(TokenPurchase.CryptoExpiredCode);
        p.IsCryptoExpired(Now.AddDays(10), 3).Should().BeFalse("ya no está esperando");
    }

    [Theory]
    [InlineData(TokenPurchaseStatus.Pending)]
    [InlineData(TokenPurchaseStatus.NeedsReconciliation)]
    [InlineData(TokenPurchaseStatus.Completed)]
    [InlineData(TokenPurchaseStatus.Failed)]
    [InlineData(TokenPurchaseStatus.Cancelled)]
    public void Solo_una_cripto_sin_confirmar_se_cancela_rechaza_o_caduca(TokenPurchaseStatus status)
    {
        TokenPurchase P() => new() { Status = status };

        ((Action)(() => P().CancelByMember(Now, "user-1"))).Should().Throw<InvalidOperationException>();
        ((Action)(() => P().Reject("x", Now, "admin-1"))).Should().Throw<InvalidOperationException>();
        ((Action)(() => P().Expire(Now, "job"))).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Notas_y_codigo_se_recortan_a_lo_que_cabe_en_la_columna()
    {
        var p = new TokenPurchase { Status = TokenPurchaseStatus.Pending };

        p.MarkForReconciliation(new string('C', 100), new string('n', 900), Now, "user-1");

        p.FailureCode.Should().HaveLength(TokenPurchase.FailureCodeMaxLength);
        p.Notes.Should().HaveLength(TokenPurchase.NotesMaxLength);

        p.Notes = null;
        p.Notes.Should().BeNull();
    }
}
