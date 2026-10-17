using MLMConquerorGlobalEdition.Billing.Jobs;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;

namespace MLMConquerorGlobalEdition.Billing.Extensions;

public static class AnnualFeeServiceCollectionExtensions
{
    /// <summary>
    /// Registra la cuota anual (Plan K). Va en un método para que la prueba de DI ejercite este mismo
    /// registro (AnnualFeeDiTests). Supone lo que Program.cs registra antes: el reloj de Billing,
    /// AddPayoutServices (el candado de fondos), AddTokenPurchaseServices (los fondos eWallet),
    /// ICommissionBalanceService e IRecurringBillingScheduler.
    /// </summary>
    public static IServiceCollection AddAnnualFeeServices(this IServiceCollection services)
    {
        services.AddScoped<IAnnualFeeStateEnrollment, AnnualFeeStateEnrollment>();
        services.AddScoped<IAnnualFeeTokenConsumer, AnnualFeeTokenConsumer>();
        services.AddScoped<IAnnualFeeChargeService, AnnualFeeChargeService>();
        services.AddScoped<AnnualFeeStateEnrollmentJob>();
        services.AddScoped<AnnualFeeStalePendingJob>();
        return services;
    }
}
