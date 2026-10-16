using Microsoft.Extensions.DependencyInjection.Extensions;
using MLMConquerorGlobalEdition.Billing.Jobs;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Tokens;
using MLMConquerorGlobalEdition.Repository.Services.Crypto;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;

namespace MLMConquerorGlobalEdition.Billing.Extensions;

public static class TokenPurchaseServiceCollectionExtensions
{
    /// <summary>
    /// Registra la compra de tokens (Plan F): precio, cupo, emisor, fondos eWallet, recibos, comprobación
    /// de step-up, direcciones de depósito cripto, el servicio de compra y sus dos trabajos: la caducidad
    /// de la cripto sin pagar y el barrido de las Pending olvidadas.
    /// </summary>
    /// <remarks>
    /// Va en un método y no suelto en Program.cs para que la prueba de DI ejercite este mismo registro.
    /// Supone lo que Program.cs ya registra antes: el reloj de Billing, la caché, las notificaciones,
    /// el ruteo de tarjeta, ICommissionBalanceService y AddPayoutServices, que aporta IEWalletClient y
    /// el candado de fondos (IMemberFundsGate), compartido con los pagos.
    /// </remarks>
    public static IServiceCollection AddTokenPurchaseServices(this IServiceCollection services)
    {
        // StepUpGrantValidator pide el reloj de SharedKernel; Billing tiene el suyo. Un adaptador sobre
        // el mismo reloj en vez de un segundo proveedor que pudiera discrepar. TryAdd por si el anfitrión
        // ya registró uno.
        services.TryAddSingleton<MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider>(
            sp => new SharedClockAdapter(sp.GetRequiredService<IDateTimeProvider>()));
        // Singleton: solo guarda la llave pública y lee el reloj, que también es singleton.
        services.TryAddSingleton<StepUpGrantValidator>();
        services.AddSingleton<IStepUpGrantCheck, StepUpGrantCheck>();

        services.AddScoped<ICryptoDepositAddressReader, CryptoDepositAddressReader>();
        services.AddScoped<ITokenPricingService, TokenPricingService>();
        services.AddScoped<ITokenMonthlyQuota, TokenMonthlyQuota>();
        services.AddScoped<ITokenIssuer, TokenIssuer>();
        services.AddScoped<IEWalletFunds, EWalletFunds>();
        services.AddScoped<ITokenPurchaseReceipts, TokenPurchaseReceipts>();
        services.AddScoped<ITokenPurchaseService, TokenPurchaseService>();

        services.AddScoped<TokenPurchaseCryptoExpiryJob>();
        services.AddScoped<TokenPurchaseStalePendingJob>();
        return services;
    }
}
