using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MLMConquerorGlobalEdition.Billing.Controllers;
using MLMConquerorGlobalEdition.Billing.Extensions;
using MLMConquerorGlobalEdition.Billing.Jobs;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Crypto;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;

namespace MLMConquerorGlobalEdition.Billing.Tests.DI;

/// <summary>
/// La compra de tokens se resuelve entera con el registro real (AddTokenPurchaseServices, el mismo
/// que llama Program.cs) sobre lo que Program.cs ya registra. Si falta una pieza, falla aquí y no en
/// la primera compra. Redis, Firebase y el correo son infraestructura: van simulados.
/// </summary>
public class TokenPurchaseDiTests
{
    private static ServiceProvider Proveedor(bool conTokens = true)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReceiptStorage:LocalPath"]        = Path.Combine(Path.GetTempPath(), "di-test-receipts-tokens"),
            ["ReceiptStorage:PublicBaseUrl"]    = "https://localhost:7001",
            ["DataProtection:Certificate:Path"] = Path.Combine(Path.GetTempPath(), "di-test-keyring", "cert.pfx"),
            // El validador de step-up lee lo mismo que la autenticación JWT de Billing.
            ["Jwt:Issuer"]          = "MLMConquerorGlobalEdition",
            ["Jwt:Audience"]        = "MLMConquerorGlobalEdition.Clients",
            ["Jwt:PublicKeyBase64"] = Convert.ToBase64String(
                System.Security.Cryptography.RSA.Create(2048).ExportSubjectPublicKeyInfo())
        }).Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(
            new Microsoft.Extensions.Hosting.Internal.HostingEnvironment
            {
                EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
                ApplicationName = "DiTests",
                ContentRootPath = Path.GetTempPath()
            });

        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("di-tokens-" + Guid.NewGuid().ToString("N")));

        // ── Lo que Program.cs registra antes de la compra de tokens ──────────
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddDistributedMemoryCache(); // Program.cs: AddStackExchangeRedisCache
        services.AddSingleton(Mock.Of<MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService>());
        services.AddSingleton(Mock.Of<Hangfire.IBackgroundJobClient>()); // Program.cs: AddHangfire
        services.AddSingleton(Mock.Of<MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService>());
        services.AddTransient<MLMConquerorGlobalEdition.SharedKernel.Interfaces.IEmailService,
                              MLMConquerorGlobalEdition.SharedKernel.Services.NullEmailService>();

        services.AddScoped<SpreedlyCardGatewayService>();
        services.AddScoped<ICardGatewayService>(sp => sp.GetRequiredService<SpreedlyCardGatewayService>());
        services.AddScoped<ICardGatewayResolver, CardGatewayResolver>();
        services.AddScoped<IGatewaySplitSelector, GatewaySplitSelector>();
        services.AddScoped<IGatewayRouter, GatewayRouter>();
        services.AddScoped<IGatewayChargeOrchestrator, GatewayChargeOrchestrator>();
        services.AddSingleton<ICardBrandDetector, CardBrandDetector>();
        services.AddHttpClient("CurrencyConverter");
        services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();
        services.AddScoped<ICommissionBalanceService, CommissionBalanceService>();

        services.AddPayoutServices();

        // ── Lo que se prueba ──────────────────────────────────────────────────
        if (conTokens) services.AddTokenPurchaseServices();

        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(typeof(ITokenPurchaseService))]
    [InlineData(typeof(ITokenPricingService))]
    [InlineData(typeof(ITokenMonthlyQuota))]
    [InlineData(typeof(ITokenIssuer))]
    [InlineData(typeof(IEWalletFunds))]
    [InlineData(typeof(ITokenPurchaseReceipts))]
    [InlineData(typeof(IMemberFundsGate))]
    [InlineData(typeof(ICryptoDepositAddressReader))]
    [InlineData(typeof(IStepUpGrantCheck))]
    [InlineData(typeof(StepUpGrantValidator))]
    [InlineData(typeof(MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider))]
    [InlineData(typeof(TokenPurchaseCryptoExpiryJob))]
    public void Cada_pieza_de_la_compra_se_resuelve(Type servicio)
    {
        using var provider = Proveedor();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService(servicio).Should().NotBeNull();
    }

    [Fact]
    public void El_controlador_se_construye_con_lo_registrado()
    {
        using var provider = Proveedor();
        using var scope = provider.CreateScope();

        ActivatorUtilities.CreateInstance<TokenPurchasesController>(scope.ServiceProvider).Should().NotBeNull();
    }

    [Fact]
    public void El_reloj_compartido_es_el_de_Billing()
    {
        using var provider = Proveedor();

        provider.GetRequiredService<MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider>()
                .Should().BeOfType<SharedClockAdapter>();
    }

    [Fact]
    public void Sin_el_registro_la_compra_no_se_resuelve()
    {
        // Contraprueba: la de arriba no pasa por casualidad.
        using var provider = Proveedor(conTokens: false);
        using var scope = provider.CreateScope();

        var act = () => scope.ServiceProvider.GetRequiredService<ITokenPurchaseService>();
        act.Should().Throw<InvalidOperationException>();
    }
}
