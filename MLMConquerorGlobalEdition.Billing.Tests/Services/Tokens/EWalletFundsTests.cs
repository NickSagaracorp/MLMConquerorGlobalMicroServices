using FluentAssertions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Payout.EWallet;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Tokens;

/// <summary>
/// Como el legado: el débito se da por hecho solo si el saldo bajó. Si i-Payout dijo que sí pero el
/// saldo no se movió (PROCESSING/PENDING), o no se sabe qué pasó, la compra va a conciliación.
/// </summary>
public class EWalletFundsTests
{
    private readonly Mock<IEWalletClient> _client = new();

    private EWalletFunds Sut() => new(TestDbContextFactory.Create(), _client.Object);

    private void Deduccion(EWalletDeductionStatus status, string? code = null) =>
        _client.Setup(c => c.DeductAsync("AMB-1", 100m, "p1", "d", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new EWalletDeduction(status, code, null));

    private void SaldoDespues(decimal saldo) =>
        _client.Setup(c => c.GetBalanceAsync("AMB-1", "USD", It.IsAny<CancellationToken>()))
               .ReturnsAsync(Result<decimal>.Success(saldo));

    [Fact]
    public async Task Aceptado_y_con_el_saldo_bajado_es_cobrado()
    {
        Deduccion(EWalletDeductionStatus.Accepted);
        SaldoDespues(400m);

        (await Sut().DebitAsync("AMB-1", 100m, 500m, "p1", "d")).Status.Should().Be(EWalletDebitStatus.Completed);
    }

    [Fact]
    public async Task Aceptado_pero_el_saldo_no_bajo_queda_a_conciliar()
    {
        Deduccion(EWalletDeductionStatus.Accepted);
        SaldoDespues(500m);

        var o = await Sut().DebitAsync("AMB-1", 100m, 500m, "p1", "d");

        o.Status.Should().Be(EWalletDebitStatus.NeedsReconciliation);
        o.Code.Should().Be("EWALLET_DEBIT_PENDING");
    }

    [Fact]
    public async Task Desconocido_queda_a_conciliar_sin_mirar_el_saldo()
    {
        Deduccion(EWalletDeductionStatus.Unknown, "EWALLET_NO_RESPONSE");

        (await Sut().DebitAsync("AMB-1", 100m, 500m, "p1", "d")).Status.Should().Be(EWalletDebitStatus.NeedsReconciliation);
        _client.Verify(c => c.GetBalanceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rechazado_es_rechazado()
    {
        Deduccion(EWalletDeductionStatus.Rejected, "EWALLET_ITEM_-5");

        var o = await Sut().DebitAsync("AMB-1", 100m, 500m, "p1", "d");

        o.Status.Should().Be(EWalletDebitStatus.Rejected);
        o.Code.Should().Be("EWALLET_ITEM_-5");
    }

    [Fact]
    public async Task El_usuario_es_el_AccountIdentifier_de_la_eWallet_preferida_primero()
    {
        var db = TestDbContextFactory.Create();
        MemberProfilesWallet W(string id, bool pref, string? acc, WalletType t = WalletType.eWallet) => new()
        {
            Id = id, MemberId = "AMB-1", WalletType = t, IsPreferred = pref, AccountIdentifier = acc,
            Status = WalletStatus.Approved, CreatedBy = "seed", CreationDate = DateTime.Now
        };
        db.Wallets.AddRange(W("a", false, "ipayout-old"), W("b", true, "ipayout-main"), W("c", true, "x@y.com", WalletType.Volet));
        await db.SaveChangesAsync();

        (await new EWalletFunds(db, _client.Object).UserNameOfAsync("AMB-1")).Should().Be("ipayout-main");
        (await new EWalletFunds(db, _client.Object).UserNameOfAsync("AMB-404")).Should().BeNull();
    }

    [Theory]
    [InlineData(400.01, EWalletDebitStatus.Completed)]           // justo en la holgura de un centavo
    [InlineData(400.02, EWalletDebitStatus.NeedsReconciliation)] // un centavo más allá
    public async Task La_holgura_es_de_un_centavo_exacto(double saldoDespues, EWalletDebitStatus esperado)
    {
        Deduccion(EWalletDeductionStatus.Accepted);
        SaldoDespues((decimal)saldoDespues);

        (await Sut().DebitAsync("AMB-1", 100m, 500m, "p1", "d")).Status.Should().Be(esperado);
    }

    [Fact]
    public async Task Aceptado_y_sin_poder_releer_el_saldo_queda_a_conciliar()
    {
        Deduccion(EWalletDeductionStatus.Accepted);
        _client.Setup(c => c.GetBalanceAsync("AMB-1", "USD", It.IsAny<CancellationToken>()))
               .ReturnsAsync(Result<decimal>.Failure("EWALLET_HTTP_503", "i-Payout get balance failed (503): <html>cuerpo crudo</html>"));

        var o = await Sut().DebitAsync("AMB-1", 100m, 500m, "p1", "d");

        o.Status.Should().Be(EWalletDebitStatus.NeedsReconciliation);
        o.Code.Should().Be("EWALLET_BALANCE_UNVERIFIED");
        o.Detail.Should().Contain("EWALLET_HTTP_503").And.NotContain("<html>", "a las notas no va el cuerpo crudo");
    }

    [Fact]
    public async Task Cobra_y_verifica_el_mismo_importe_redondeado_a_centavos()
    {
        _client.Setup(c => c.DeductAsync("AMB-1", 100.01m, "p1", "d", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new EWalletDeduction(EWalletDeductionStatus.Accepted, null, null));
        SaldoDespues(399.99m);

        var o = await Sut().DebitAsync("AMB-1", 100.005m, 500m, "p1", "d");

        o.Status.Should().Be(EWalletDebitStatus.Completed);
        _client.Verify(c => c.DeductAsync("AMB-1", 100.01m, "p1", "d", It.IsAny<CancellationToken>()), Times.Once);
        EWalletFunds.ChargeAmount(99.994m).Should().Be(99.99m);
        EWalletFunds.ChargeAmount(99.995m).Should().Be(100.00m);
    }

    [Fact]
    public async Task Solo_vale_una_eWallet_aprobada()
    {
        var db = TestDbContextFactory.Create();
        MemberProfilesWallet W(string id, bool pref, string acc, WalletStatus status) => new()
        {
            Id = id, MemberId = "AMB-1", WalletType = WalletType.eWallet, IsPreferred = pref, AccountIdentifier = acc,
            Status = status, CreatedBy = "seed", CreationDate = DateTime.Now
        };
        db.Wallets.AddRange(W("a", true, "ipayout-pending", WalletStatus.Pending), W("b", false, "ipayout-ok", WalletStatus.Approved));
        db.Wallets.Add(new MemberProfilesWallet
        {
            Id = "c", MemberId = "AMB-2", WalletType = WalletType.eWallet, IsPreferred = true, AccountIdentifier = "rechazada",
            Status = WalletStatus.Rejected, CreatedBy = "seed", CreationDate = DateTime.Now
        });
        await db.SaveChangesAsync();

        (await new EWalletFunds(db, _client.Object).UserNameOfAsync("AMB-1")).Should().Be("ipayout-ok");
        (await new EWalletFunds(db, _client.Object).UserNameOfAsync("AMB-2")).Should().BeNull();
    }
}
