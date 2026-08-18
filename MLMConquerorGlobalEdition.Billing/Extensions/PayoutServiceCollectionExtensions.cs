using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Services;
using MLMConquerorGlobalEdition.Billing.Services.Payout;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Anchoring;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Csv;
using MLMConquerorGlobalEdition.Repository.Services.Payout.EWallet;
using MLMConquerorGlobalEdition.Repository.Services.Payout.PayQuicker;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;
using MLMConquerorGlobalEdition.Repository.Services.Wallets;
using MLMConquerorGlobalEdition.Repository.Services.Payout;

namespace MLMConquerorGlobalEdition.Billing.Extensions;

public static class PayoutServiceCollectionExtensions
{
    /// <summary>
    /// Registra el pipeline COMPLETO de payouts: los gateways (vía AddPayoutGatewayClients)
    /// más el orquestador, recibos, CSV, lotes y reconciliación.
    /// Lo llaman Billing y AdminAPI.
    /// </summary>
    public static IServiceCollection AddPayoutServices(this IServiceCollection services)
    {
        services.AddPayoutGatewayClients();

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
