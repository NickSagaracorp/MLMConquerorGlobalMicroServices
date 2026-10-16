using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Jobs;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Jobs;

/// <summary>La caducidad de la cripto sin pagar lee el plazo de configuración y, si falta, usa 3 días (D18).</summary>
public class TokenPurchaseCryptoExpiryJobTests
{
    private static TokenPurchaseCryptoExpiryJob Job(Mock<ITokenPurchaseService> svc, Dictionary<string, string?> values) =>
        new(svc.Object, new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            NullLogger<TokenPurchaseCryptoExpiryJob>.Instance);

    [Fact]
    public async Task Sin_clave_usa_el_plazo_por_defecto()
    {
        var svc = new Mock<ITokenPurchaseService>();

        await Job(svc, new()).ExecuteAsync();

        svc.Verify(s => s.ExpireStaleCryptoAsync(TokenPurchaseRules.DefaultCryptoExpiryDays, It.IsAny<string>(), It.IsAny<CancellationToken>()));
        TokenPurchaseRules.DefaultCryptoExpiryDays.Should().Be(3);
    }

    [Fact]
    public async Task Con_clave_usa_el_plazo_configurado()
    {
        var svc = new Mock<ITokenPurchaseService>();

        await Job(svc, new() { [TokenPurchaseRules.CryptoExpiryDaysConfigKey] = "7" }).ExecuteAsync();

        svc.Verify(s => s.ExpireStaleCryptoAsync(7, It.Is<string>(a => !string.IsNullOrWhiteSpace(a)), It.IsAny<CancellationToken>()));
    }
}
