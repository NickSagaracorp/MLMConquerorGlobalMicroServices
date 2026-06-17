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
    /// Registers the outbound payout gateways, resolver, orchestrator and receipt services.
    /// Called by both the Billing API and the AdminAPI (which references Billing).
    /// </summary>
    public static IServiceCollection AddPayoutServices(this IServiceCollection services)
    {
        services.AddScoped<IPayoutGatewayService, EWalletPayoutGatewayService>();
        services.AddScoped<IPayoutGatewayService, VoletPayoutGatewayService>();
        services.AddScoped<IPayoutGatewayResolver, PayoutGatewayResolver>();
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
