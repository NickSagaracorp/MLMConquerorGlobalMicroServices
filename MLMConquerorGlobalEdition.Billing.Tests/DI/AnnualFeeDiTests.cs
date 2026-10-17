using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MLMConquerorGlobalEdition.Billing.Controllers;
using MLMConquerorGlobalEdition.Billing.Extensions;
using MLMConquerorGlobalEdition.Billing.Jobs;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Tests.DI;

/// <summary>
/// La cuota anual y el motor se resuelven con el registro real (AddAnnualFeeServices, el mismo que llama
/// Program.cs) sobre lo que Program.cs registra antes. Si falta una pieza, falla aquí y no de madrugada.
/// </summary>
public class AnnualFeeDiTests
{
    private static ServiceProvider Proveedor(bool conCuota = true)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReceiptStorage:LocalPath"]        = Path.Combine(Path.GetTempPath(), "di-test-receipts-cuota"),
            ["ReceiptStorage:PublicBaseUrl"]    = "https://localhost:7001",
            ["DataProtection:Certificate:Path"] = Path.Combine(Path.GetTempPath(), "di-test-keyring", "cert.pfx"),
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

        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("di-cuota-" + Guid.NewGuid().ToString("N")));

        // ── Lo que Program.cs registra antes de la cuota anual ───────────────
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddDistributedMemoryCache();
        services.AddSingleton(Mock.Of<MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService>());
        services.AddSingleton(Mock.Of<Hangfire.IBackgroundJobClient>());
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
        services.AddScoped<IRecurringBillingScheduler, RecurringBillingScheduler>();
        services.AddScoped<ICommissionBalanceService, CommissionBalanceService>();
        services.AddScoped<IRecurringBillingProcessor, RecurringBillingProcessor>();

        services.AddPayoutServices();
        services.AddTokenPurchaseServices();

        // ── Lo que se prueba ──────────────────────────────────────────────────
        if (conCuota) services.AddAnnualFeeServices();

        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(typeof(IAnnualFeeChargeService))]
    [InlineData(typeof(IAnnualFeeStateEnrollment))]
    [InlineData(typeof(IAnnualFeeTokenConsumer))]
    [InlineData(typeof(AnnualFeeStateEnrollmentJob))]
    [InlineData(typeof(AnnualFeeStalePendingJob))]
    [InlineData(typeof(IRecurringBillingProcessor))]
    public void Cada_pieza_se_resuelve(Type servicio)
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

        ActivatorUtilities.CreateInstance<AnnualFeeController>(scope.ServiceProvider).Should().NotBeNull();
    }

    [Fact]
    public void Sin_el_registro_el_motor_no_se_resuelve()
    {
        using var provider = Proveedor(conCuota: false);
        using var scope = provider.CreateScope();

        var act = () => scope.ServiceProvider.GetRequiredService<IRecurringBillingProcessor>();
        act.Should().Throw<InvalidOperationException>();
    }
}
