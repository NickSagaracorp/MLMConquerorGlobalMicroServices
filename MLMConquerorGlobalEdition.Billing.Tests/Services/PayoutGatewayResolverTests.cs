using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Billing.Services.Payout;
using MLMConquerorGlobalEdition.Repository.Services.Payout.EWallet;
using MLMConquerorGlobalEdition.Repository.Services.Payout.PayQuicker;
using MLMConquerorGlobalEdition.Domain.Enums;
using Moq;
using Xunit;
using MLMConquerorGlobalEdition.Repository.Services.Payout;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

public class PayoutGatewayResolverTests
{
    // El resolver sólo mira GatewayType, así que las dependencias van mockeadas: no se
    // ejercita ninguna llamada al proveedor acá.
    private static PayoutGatewayResolver CreateResolver() =>
        new(new IPayoutGatewayService[]
        {
            new EWalletPayoutGatewayService(
                Mock.Of<IEWalletClient>(),
                NullLogger<EWalletPayoutGatewayService>.Instance),
            new VoletPayoutGatewayService(
                Mock.Of<MLMConquerorGlobalEdition.Repository.Services.Payout.Volet.IVoletClient>(),
                NullLogger<VoletPayoutGatewayService>.Instance),
            new PayQuickerPayoutGatewayService(
                Mock.Of<IPayQuickerSettingsProvider>(),
                Array.Empty<IPayQuickerClient>(),
                NullLogger<PayQuickerPayoutGatewayService>.Instance)
        });

    [Fact]
    public void Resolve_KnownWalletType_ReturnsMatchingGateway()
    {
        var result = CreateResolver().Resolve(WalletType.Volet);

        result.IsSuccess.Should().BeTrue();
        result.Value!.GatewayType.Should().Be(WalletType.Volet);
    }

    [Fact]
    public void Resolve_PayQuicker_ReturnsPayQuickerGateway()
    {
        var result = CreateResolver().Resolve(WalletType.PayQuicker);

        result.IsSuccess.Should().BeTrue();
        result.Value!.GatewayType.Should().Be(WalletType.PayQuicker);
    }

    [Fact]
    public void Resolve_UnknownWalletType_ReturnsFailureNotException()
    {
        var result = CreateResolver().Resolve(WalletType.Crypto);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PAYOUT_GATEWAY_NOT_SUPPORTED");
    }
}
