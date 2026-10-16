using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Jobs;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Jobs;

/// <summary>El barrido de las Pending olvidadas lee el plazo de configuración y, si falta, usa 30 minutos.</summary>
public class TokenPurchaseStalePendingJobTests
{
    private static TokenPurchaseStalePendingJob Job(Mock<ITokenPurchaseService> svc, Dictionary<string, string?> values) =>
        new(svc.Object, new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            NullLogger<TokenPurchaseStalePendingJob>.Instance);

    [Fact]
    public async Task Sin_clave_usa_el_plazo_por_defecto()
    {
        var svc = new Mock<ITokenPurchaseService>();

        await Job(svc, new()).ExecuteAsync();

        svc.Verify(s => s.MoveStalePendingToReconciliationAsync(TokenPurchaseRules.DefaultStalePendingMinutes, It.IsAny<string>(), It.IsAny<CancellationToken>()));
        TokenPurchaseRules.DefaultStalePendingMinutes.Should().Be(30);
    }

    [Fact]
    public async Task Con_clave_usa_el_plazo_configurado()
    {
        var svc = new Mock<ITokenPurchaseService>();

        await Job(svc, new() { [TokenPurchaseRules.StalePendingMinutesConfigKey] = "45" }).ExecuteAsync();

        svc.Verify(s => s.MoveStalePendingToReconciliationAsync(45, It.Is<string>(a => !string.IsNullOrWhiteSpace(a)), It.IsAny<CancellationToken>()));
    }
}
