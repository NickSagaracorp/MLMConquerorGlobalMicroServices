using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MLMConquerorGlobalEdition.Billing.Services.Payout;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Anchoring;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Csv;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;

namespace MLMConquerorGlobalEdition.Billing.Extensions;

public static class PayoutServiceCollectionExtensions
{
    /// <summary>
    /// Registers just the outbound payout gateways + resolver (i-payout/Volet, IPayoutGatewayResolver).
    /// No dependency on ICurrentUserService, IReceiptStorage config, etc. — safe for any host that
    /// only needs to call a gateway directly (e.g. SignupAPI registering the new eWallet account).
    /// Called by AddPayoutServices() below as well as SignupAPI/Program.cs directly.
    /// </summary>
    public static IServiceCollection AddPayoutGatewayServices(this IServiceCollection services)
    {
        // i-payout sandbox/production credentials — "IPayout" section, filled in via appsettings.json.
        services.AddOptions<IPayoutOptions>().BindConfiguration("IPayout");

        // Typed HttpClient — EWalletPayoutGatewayService calls the real i-payout
        // ws_JsonAdapter.aspx endpoint (BaseUrl comes from IPayoutOptions).
        services.AddHttpClient<EWalletPayoutGatewayService>();
        services.AddScoped<IPayoutGatewayService>(sp => sp.GetRequiredService<EWalletPayoutGatewayService>());
        services.AddScoped<IPayoutGatewayService, VoletPayoutGatewayService>();
        services.AddScoped<IPayoutGatewayResolver, PayoutGatewayResolver>();

        return services;
    }

    /// <summary>
    /// Registers the outbound payout gateways, resolver, orchestrator and receipt services.
    /// Called by both the Billing API and the AdminAPI (which references Billing). Requires
    /// ICurrentUserService and ReceiptStorage:* config to already be registered by the host.
    /// </summary>
    public static IServiceCollection AddPayoutServices(this IServiceCollection services)
    {
        services.AddPayoutGatewayServices();
        services.AddScoped<IPayoutOrchestrator, PayoutOrchestrator>();

        services.AddSingleton<IReceiptPdfRenderer, ITextReceiptPdfRenderer>();
        services.AddScoped<IPayoutReceiptService, PayoutReceiptService>();
        services.AddScoped<IReceiptStorage>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var path = cfg["ReceiptStorage:LocalPath"]
                ?? Path.Combine(AppContext.BaseDirectory, "payout-receipts");
            var baseUrl = cfg["ReceiptStorage:PublicBaseUrl"] ?? "https://localhost:7001";
            return new LocalReceiptStorage(path, baseUrl);
        });

        // Anchoring + verification (Task 3 & 4)
        services.AddScoped<IDocumentAnchorService, StubDocumentAnchorService>();
        services.AddScoped<IReceiptVerificationService, ReceiptVerificationService>();

        // CSV adapters + resolver (Sprint 19 Task 2)
        services.AddScoped<EWalletPayoutCsvAdapter>();
        services.AddScoped<VoletPayoutCsvAdapter>();
        services.AddScoped<IPayoutCsvFormatter>(sp => sp.GetRequiredService<EWalletPayoutCsvAdapter>());
        services.AddScoped<IPayoutCsvFormatter>(sp => sp.GetRequiredService<VoletPayoutCsvAdapter>());
        services.AddScoped<IPayoutResultCsvParser>(sp => sp.GetRequiredService<EWalletPayoutCsvAdapter>());
        services.AddScoped<IPayoutResultCsvParser>(sp => sp.GetRequiredService<VoletPayoutCsvAdapter>());
        services.AddScoped<IPayoutCsvResolver, PayoutCsvResolver>();

        // Batch services (Sprint 19 Task 3 + 4)
        services.AddScoped<IPayoutBatchExportService, PayoutBatchExportService>();
        services.AddScoped<IPayoutBatchReconciliationService, PayoutBatchReconciliationService>();

        // Stale-attempt reconciliation sweep (money-safety net for crash-after-disburse)
        services.AddScoped<IPayoutReconciliationService, PayoutReconciliationService>();

        return services;
    }
}
