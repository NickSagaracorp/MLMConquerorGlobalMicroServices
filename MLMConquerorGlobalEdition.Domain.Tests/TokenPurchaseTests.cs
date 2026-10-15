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
}
