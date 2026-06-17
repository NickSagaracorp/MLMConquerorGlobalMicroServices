using FluentAssertions;
using MLMConquerorGlobalEdition.Billing.Services.Payout;
using MLMConquerorGlobalEdition.Domain.Enums;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

public class PayoutGatewayResolverTests
{
    private static PayoutGatewayResolver CreateResolver() =>
        new(new IPayoutGatewayService[]
        {
            new EWalletPayoutGatewayService(),
            new VoletPayoutGatewayService()
        });

    [Fact]
    public void Resolve_KnownWalletType_ReturnsMatchingGateway()
    {
        var result = CreateResolver().Resolve(WalletType.Volet);

        result.IsSuccess.Should().BeTrue();
        result.Value!.GatewayType.Should().Be(WalletType.Volet);
    }

    [Fact]
    public void Resolve_UnknownWalletType_ReturnsFailureNotException()
    {
        var result = CreateResolver().Resolve(WalletType.Crypto);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PAYOUT_GATEWAY_NOT_SUPPORTED");
    }
}
